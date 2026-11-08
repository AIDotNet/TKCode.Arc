/* TKCode.Arc.Video.Native, macOS: native/video/tkcode_video.h over AVPlayer + AVPlayerItemVideoOutput (Objective-C, ARC).
   Design: design/components/video/video.md section 5.2; ADR 0036. Build: tools/video/build-mac.sh.

   Threading:
   - Each player owns a private serial queue. Every control call (open, play, pause, seek, set_*) runs on it with
     dispatch_sync, and every event is emitted from it, so the state below is only touched there.
   - KVO, the end-of-item notification, seek completions and asset loading may arrive on any thread; they all hop to the
     private queue, and a 100 ms timer on it also re-reads the item's state (some changes are only announced on the main
     queue). The timer only compares and emits changes; it does not drive playback.
   - AVPlayer itself needs the main dispatch queue to be serviced: without it the item never becomes ready and time does
     not advance (measured on macOS 26 with a console host whose main thread never runs a run loop). Avalonia apps run
     NSApplication on the main thread, so this holds there; a host that never services the main queue gets
     TKV_EVENT_ERROR (TKV_ERR_ABORTED, with a message saying so) after kMainQueueTimeout instead of loading forever.
   - tkv_player_acquire_frame / release_frame run on the caller's (UI) thread and only touch the video output and the
     held pixel buffer, under frame_lock.
   - tkv_player_destroy marks the player destroyed on the private queue; events check the flag on that queue, so none
     follow after it returns. */
#import <Accelerate/Accelerate.h>
#import <AVFoundation/AVFoundation.h>
#import <CoreMedia/CoreMedia.h>
#import <CoreVideo/CoreVideo.h>
#import <Foundation/Foundation.h>
#import <IOSurface/IOSurfaceRef.h>
#import <Metal/Metal.h>
#import <QuartzCore/QuartzCore.h>
#include <TargetConditionals.h>
#include <os/lock.h>
#include <math.h>
#include <stdlib.h>
#include <string.h>

#include "../tkcode_video.h"

#include "../tkv_scale.h"

static void* const kQueueKey = (void*)&kQueueKey;
static void* const kKvoContext = (void*)&kKvoContext;

/* How long the player waits for the first frame after a load or a seek before giving up on TKV_EVENT_FRAME. */
static const double kFramePendingTimeout = 3.0;

/* How long a load waits when the main dispatch queue has never run a block before reporting the host problem. */
static const double kMainQueueTimeout = 5.0;

/* Set by a block queued on the main queue; stays NO in a host that never services the main queue. */
static volatile BOOL s_mainQueueAlive = NO;

static void ProbeMainQueue(void)
{
    if (!s_mainQueueAlive)
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            s_mainQueueAlive = YES;
        });
    }
}

/* How long a buffer presented with a timeline semaphore waits for the compositor's signal before it is returned anyway. */
static const double kPendingTimeout = 1.0;

/* One MTLSharedEvent per pooled IOSurface (by IOSurfaceGetID), so each timeline only moves forward. */
@interface TKVImageSync : NSObject
{
@public
    id<MTLSharedEvent> event;
    uint64_t lastSignal;
}
@end

@implementation TKVImageSync
@end

/* A pixel buffer kept out of the pool until the compositor has signalled value on event. */
@interface TKVPendingBuffer : NSObject
{
@public
    CVPixelBufferRef buffer;
    id<MTLSharedEvent> event;
    uint64_t value;
    double since;
}
@end

@implementation TKVPendingBuffer
- (void)dealloc
{
    if (buffer)
    {
        CVPixelBufferRelease(buffer);
    }
}
@end

/* The Metal device whose IOKit registry id is luid (8 bytes, big-endian: Avalonia's ICompositionGpuInterop.DeviceLuid on
   Metal); the system default device when luid is NULL or matches none. */
static id<MTLDevice> DeviceForLuid(const uint8_t* luid)
{
    if (luid)
    {
        uint64_t registryId = 0;
        for (int i = 0; i < 8; i++)
        {
            registryId = (registryId << 8) | luid[i];
        }
#if TARGET_OS_OSX
        NSArray<id<MTLDevice>>* devices = MTLCopyAllDevices();
        for (id<MTLDevice> device in devices)
        {
            if (device.registryID == registryId)
            {
                return device;
            }
        }
#else
        (void)registryId; /* iOS has one GPU. */
#endif
    }
    return MTLCreateSystemDefaultDevice();
}

@interface TKVPlayer : NSObject <AVPlayerItemOutputPullDelegate, AVPlayerItemLegibleOutputPushDelegate>
@end

@implementation TKVPlayer {
@public
    tkv_event_callback _callback;
    void* _context;
    dispatch_queue_t _queue;
    dispatch_source_t _timer;
    AVPlayer* _player;
    AVURLAsset* _asset;
    AVPlayerItem* _item;
    AVPlayerItemVideoOutput* _output;
    id _timeObserver;
    id _endObserver;
    BOOL _destroyed;
    BOOL _observing;

    /* Private-queue state. */
    uint64_t _openGeneration;
    uint64_t _seekGeneration;
    int32_t _state;
    BOOL _ready;
    BOOL _failed;
    BOOL _wantPlay;
    BOOL _loop;
    BOOL _endHandled;
    double _rate;
    float _volume;
    double _duration;
    int32_t _width;
    int32_t _height;
    int32_t _reportedWidth;
    int32_t _reportedHeight;
    int32_t _rotation;

    /* Embedded text tracks (the legible media selection group), on _queue. */
    AVMediaSelectionGroup* _legibleGroup;
    NSArray<AVMediaSelectionOption*>* _textOptions;
    AVPlayerItemLegibleOutput* _legibleOutput;
    int32_t _selectedText;
    tkv_text_callback _textCallback;
    void* _textContext;
    int32_t _seekable;
    NSArray<NSValue*>* _buffered;
    BOOL _framePending;
    double _framePendingSince;
    double _openedAt;

    /* Read from any thread. */
    volatile BOOL _seeking;
    volatile double _seekTarget;

    /* Frame state, under _frameLock. */
    os_unfair_lock _frameLock;
    AVPlayerItemVideoOutput* _frameOutput;
    CVPixelBufferRef _held;
    BOOL _heldLocked;
    int64_t _heldToken;
    int64_t _heldImageId;
    TKVImageSync* _heldSync;
    uint64_t _heldSyncValue;
    NSMutableDictionary<NSNumber*, TKVImageSync*>* _images;
    NSMutableArray<TKVPendingBuffer*>* _pending;
    CVPixelBufferRef _lastBuffer;
    CMTime _lastTime;
    BOOL _redeliver;
    /* CPU frames scaled down to the request's target size: the pixels handed out, and vImage's scratch memory. */
    uint8_t* _scaled;
    size_t _scaledCapacity;
    void* _scaleScratch;
    size_t _scaleScratchCapacity;
    id<MTLDevice> _device;
    int64_t _nextToken;

    /* Last error text; replaced only on a new error, so the pointer handed out stays valid until then. */
    os_unfair_lock _errorLock;
    char* _lastError;
    char* _retiredError;
}

- (instancetype)initWithCallback:(tkv_event_callback)callback context:(void*)context luid:(const uint8_t*)luid
{
    if ((self = [super init]))
    {
        _device = DeviceForLuid(luid);
        _images = [NSMutableDictionary dictionary];
        _pending = [NSMutableArray array];
        _lastTime = kCMTimeZero;
        _callback = callback;
        _context = context;
        _queue = dispatch_queue_create("dev.tkcode.arc.video.player", DISPATCH_QUEUE_SERIAL);
        dispatch_queue_set_specific(_queue, kQueueKey, (__bridge void*)self, NULL);
        _player = [[AVPlayer alloc] init];
        _player.actionAtItemEnd = AVPlayerActionAtItemEndPause;
        /* Text tracks stay off until the consumer selects one (no automatic choice by the system's language). */
        _player.appliesMediaSelectionCriteriaAutomatically = NO;
        _selectedText = -1;
        _rate = 1.0;
        _volume = 1.0f;
        _state = TKV_STATE_IDLE;
        _frameLock = OS_UNFAIR_LOCK_INIT;
        _errorLock = OS_UNFAIR_LOCK_INIT;
        _seekable = -1;
        _reportedWidth = -1;
        _reportedHeight = -1;
        [_player addObserver:self forKeyPath:@"timeControlStatus" options:0 context:kKvoContext];

        __weak TKVPlayer* weakSelf = self;
        _timer = dispatch_source_create(DISPATCH_SOURCE_TYPE_TIMER, 0, 0, _queue);
        dispatch_source_set_timer(_timer, dispatch_time(DISPATCH_TIME_NOW, 100 * NSEC_PER_MSEC), 100 * NSEC_PER_MSEC, 10 * NSEC_PER_MSEC);
        dispatch_source_set_event_handler(_timer, ^{
            [weakSelf refresh];
        });
        dispatch_resume(_timer);
    }
    return self;
}

- (void)dealloc
{
    free(_lastError);
    free(_retiredError);
    free(_scaled);
    free(_scaleScratch);
    if (_lastBuffer)
    {
        CVPixelBufferRelease(_lastBuffer);
    }
}

/* Runs block on the private queue and waits; inline when already on it. */
- (void)sync:(dispatch_block_t)block
{
    if (dispatch_get_specific(kQueueKey) == (__bridge void*)self)
    {
        block();
    }
    else
    {
        dispatch_sync(_queue, block);
    }
}

- (void)emit:(int32_t)event a:(double)a b:(double)b
{
    if (_destroyed || !_callback)
    {
        return;
    }
    _callback(_context, event, a, b);
}

- (void)setState:(int32_t)state
{
    if (_state == state)
    {
        return;
    }
    _state = state;
    [self emit:TKV_EVENT_STATE a:state b:0];
}

- (void)setError:(NSString*)message
{
    const char* utf8 = message.UTF8String ?: "unknown error";
    char* copy = strdup(utf8);
    os_unfair_lock_lock(&_errorLock);
    /* The previous text may still be read by a caller that fetched it before this error; keep it alive one more error. */
    free(_retiredError);
    _retiredError = _lastError;
    _lastError = copy;
    os_unfair_lock_unlock(&_errorLock);
}

- (const char*)lastError
{
    os_unfair_lock_lock(&_errorLock);
    const char* text = _lastError ? _lastError : "";
    os_unfair_lock_unlock(&_errorLock);
    return text;
}

static int32_t ErrorKind(NSError* error)
{
    if (!error)
    {
        return TKV_ERR_DECODE;
    }
    NSError* underlying = error.userInfo[NSUnderlyingErrorKey];
    if ([error.domain isEqualToString:NSURLErrorDomain] || [underlying.domain isEqualToString:NSURLErrorDomain])
    {
        return TKV_ERR_NETWORK;
    }
    if ([error.domain isEqualToString:NSCocoaErrorDomain] || [error.domain isEqualToString:NSPOSIXErrorDomain])
    {
        return TKV_ERR_NETWORK;
    }
    if ([error.domain isEqualToString:AVFoundationErrorDomain])
    {
        switch (error.code)
        {
            case AVErrorFileFormatNotRecognized:
            case AVErrorFormatUnsupported:
            case AVErrorContentIsUnavailable:
            case AVErrorContentIsNotAuthorized:
            case AVErrorNoLongerPlayable:
                return TKV_ERR_UNSUPPORTED;
            case AVErrorDecodeFailed:
            case AVErrorFailedToParse:
                return TKV_ERR_DECODE;
            case AVErrorFileFailedToParse:
                return TKV_ERR_DECODE;
            case AVErrorContentNotUpdated:
            case AVErrorServerIncorrectlyConfigured:
            case AVErrorNoDataCaptured:
                return TKV_ERR_NETWORK;
            default:
                break;
        }
        if (underlying && [underlying.domain isEqualToString:NSOSStatusErrorDomain] && underlying.code == -12847)
        {
            return TKV_ERR_UNSUPPORTED;
        }
        return TKV_ERR_DECODE;
    }
    return TKV_ERR_DECODE;
}

- (void)failWith:(NSError*)error kind:(int32_t)kind
{
    if (_failed)
    {
        return;
    }
    _failed = YES;
    _wantPlay = NO;
    NSString* message = error ? [NSString stringWithFormat:@"%@ (%@ %ld)", error.localizedDescription, error.domain, (long)error.code] : @"playback failed";
    NSError* underlying = error.userInfo[NSUnderlyingErrorKey];
    if (underlying)
    {
        message = [message stringByAppendingFormat:@"; %@ (%@ %ld)", underlying.localizedDescription, underlying.domain, (long)underlying.code];
    }
    [self setError:message];
    [_player pause];
    _state = TKV_STATE_FAILED;
    [self emit:TKV_EVENT_ERROR a:kind b:0];
    [self emit:TKV_EVENT_STATE a:TKV_STATE_FAILED b:0];
}

/* ---- Loading. */

- (void)detachItem
{
    if (_observing && _item)
    {
        [_item removeObserver:self forKeyPath:@"status" context:kKvoContext];
        [_item removeObserver:self forKeyPath:@"loadedTimeRanges" context:kKvoContext];
        [_item removeObserver:self forKeyPath:@"presentationSize" context:kKvoContext];
        [_item removeObserver:self forKeyPath:@"duration" context:kKvoContext];
        _observing = NO;
    }
    if (_timeObserver)
    {
        [_player removeTimeObserver:_timeObserver];
        _timeObserver = nil;
    }
    if (_endObserver)
    {
        [[NSNotificationCenter defaultCenter] removeObserver:_endObserver];
        _endObserver = nil;
    }
    [_asset cancelLoading];
    [_player pause];
    [_player replaceCurrentItemWithPlayerItem:nil];
    if (_item && _output)
    {
        [_item removeOutput:_output];
    }
    if (_item && _legibleOutput)
    {
        [_item removeOutput:_legibleOutput];
    }
    _legibleOutput = nil;
    _legibleGroup = nil;
    _textOptions = nil;
    _selectedText = -1;
    os_unfair_lock_lock(&_frameLock);
    _frameOutput = nil;
    _redeliver = NO;
    if (_lastBuffer)
    {
        CVPixelBufferRelease(_lastBuffer);
        _lastBuffer = NULL;
    }
    os_unfair_lock_unlock(&_frameLock);
    _item = nil;
    _output = nil;
    _asset = nil;
    _ready = NO;
    _failed = NO;
    _endHandled = NO;
    _framePending = NO;
    _seeking = NO;
    _duration = 0;
    _width = 0;
    _height = 0;
    _rotation = 0;
    _reportedWidth = -1;
    _reportedHeight = -1;
    _seekable = -1;
    _buffered = nil;
}

- (int32_t)open:(NSURL*)url
{
    [self detachItem];
    uint64_t generation = ++_openGeneration;
    _wantPlay = NO;
    _state = TKV_STATE_IDLE;
    [self setState:TKV_STATE_LOADING];
    _openedAt = CACurrentMediaTime();
    ProbeMainQueue();

    if (url.isFileURL && ![[NSFileManager defaultManager] fileExistsAtPath:url.path])
    {
        NSError* error = [NSError errorWithDomain:NSCocoaErrorDomain code:NSFileReadNoSuchFileError
                                         userInfo:@{ NSLocalizedDescriptionKey: [NSString stringWithFormat:@"No such file: %@", url.path] }];
        __weak TKVPlayer* weakSelf = self;
        dispatch_async(_queue, ^{
            TKVPlayer* strongSelf = weakSelf;
            if (strongSelf && !strongSelf->_destroyed && generation == strongSelf->_openGeneration)
            {
                [strongSelf failWith:error kind:TKV_ERR_NETWORK];
            }
        });
        return TKV_OK;
    }

    AVURLAsset* asset = [AVURLAsset URLAssetWithURL:url options:@{ AVURLAssetPreferPreciseDurationAndTimingKey: @YES }];
    _asset = asset;
    __weak TKVPlayer* weakSelf = self;
    NSArray<NSString*>* keys = @[ @"playable", @"tracks", @"duration", @"availableMediaCharacteristicsWithMediaSelectionOptions" ];
    [asset loadValuesAsynchronouslyForKeys:keys completionHandler:^{
        TKVPlayer* strongSelf = weakSelf;
        if (!strongSelf)
        {
            return;
        }
        dispatch_async(strongSelf->_queue, ^{
            [strongSelf assetLoaded:asset keys:keys generation:generation];
        });
    }];
    return TKV_OK;
}

- (void)assetLoaded:(AVURLAsset*)asset keys:(NSArray<NSString*>*)keys generation:(uint64_t)generation
{
    if (_destroyed || generation != _openGeneration || asset != _asset)
    {
        return;
    }
    for (NSString* key in keys)
    {
        NSError* error = nil;
        AVKeyValueStatus status = [asset statusOfValueForKey:key error:&error];
        if (status == AVKeyValueStatusFailed)
        {
            [self failWith:error kind:ErrorKind(error)];
            return;
        }
        if (status == AVKeyValueStatusCancelled)
        {
            return;
        }
    }
    if (!asset.playable)
    {
        [self failWith:[NSError errorWithDomain:AVFoundationErrorDomain code:AVErrorFormatUnsupported
                                       userInfo:@{ NSLocalizedDescriptionKey: @"The media is not playable on this system." }]
                  kind:TKV_ERR_UNSUPPORTED];
        return;
    }

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    AVAssetTrack* video = [asset tracksWithMediaType:AVMediaTypeVideo].firstObject;
#pragma clang diagnostic pop
    if (video)
    {
        CGAffineTransform t = video.preferredTransform;
        double degrees = atan2(t.b, t.a) * 180.0 / M_PI;
        int32_t rotation = ((int32_t)lround(degrees / 90.0) * 90 % 360 + 360) % 360;
        CGSize natural = video.naturalSize;
        _rotation = rotation;
        if (rotation == 90 || rotation == 270)
        {
            _width = (int32_t)lround(fabs(natural.height));
            _height = (int32_t)lround(fabs(natural.width));
        }
        else
        {
            _width = (int32_t)lround(fabs(natural.width));
            _height = (int32_t)lround(fabs(natural.height));
        }
    }

    NSDictionary* attributes = @{
        (id)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA),
        (id)kCVPixelBufferIOSurfacePropertiesKey: @{},
        (id)kCVPixelBufferMetalCompatibilityKey: @YES,
    };
    AVPlayerItemVideoOutput* output = [[AVPlayerItemVideoOutput alloc] initWithPixelBufferAttributes:attributes];
    output.suppressesPlayerRendering = YES;
    [output setDelegate:self queue:_queue];
    AVPlayerItem* item = [AVPlayerItem playerItemWithAsset:asset];
    item.audioTimePitchAlgorithm = AVAudioTimePitchAlgorithmSpectral;
    [item addOutput:output];
    _item = item;
    _output = output;
    [self attachTextTracks:asset item:item];
    os_unfair_lock_lock(&_frameLock);
    _frameOutput = output;
    os_unfair_lock_unlock(&_frameLock);

    [item addObserver:self forKeyPath:@"status" options:0 context:kKvoContext];
    [item addObserver:self forKeyPath:@"loadedTimeRanges" options:0 context:kKvoContext];
    [item addObserver:self forKeyPath:@"presentationSize" options:0 context:kKvoContext];
    [item addObserver:self forKeyPath:@"duration" options:0 context:kKvoContext];
    _observing = YES;

    __weak TKVPlayer* weakSelf = self;
    _endObserver = [[NSNotificationCenter defaultCenter] addObserverForName:AVPlayerItemDidPlayToEndTimeNotification object:item queue:nil
                                                                 usingBlock:^(NSNotification* note) {
        TKVPlayer* strongSelf = weakSelf;
        if (!strongSelf)
        {
            return;
        }
        dispatch_async(strongSelf->_queue, ^{
            if (strongSelf->_item == note.object)
            {
                [strongSelf handleEnd];
            }
        });
    }];
    _player.volume = _volume;
    [_player replaceCurrentItemWithPlayerItem:item];
    _timeObserver = [_player addPeriodicTimeObserverForInterval:CMTimeMakeWithSeconds(0.25, 600) queue:_queue usingBlock:^(CMTime time) {
        TKVPlayer* strongSelf = weakSelf;
        if (strongSelf && strongSelf->_ready && (strongSelf->_state == TKV_STATE_PLAYING || strongSelf->_state == TKV_STATE_BUFFERING))
        {
            [strongSelf emit:TKV_EVENT_POSITION a:CMTimeGetSeconds(time) b:0];
        }
    }];
    [self refresh];
}

/* ---- Embedded text tracks (on _queue). */

- (void)attachTextTracks:(AVURLAsset*)asset item:(AVPlayerItem*)item
{
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    AVMediaSelectionGroup* group = [asset mediaSelectionGroupForMediaCharacteristic:AVMediaCharacteristicLegible];
#pragma clang diagnostic pop
    /* Each subtitle track also comes as a "forced subtitles only" option of the same track; those are not tracks to offer. */
    NSMutableArray<AVMediaSelectionOption*>* options = [NSMutableArray array];
    for (AVMediaSelectionOption* option in group.options)
    {
        if (![option hasMediaCharacteristic:AVMediaCharacteristicContainsOnlyForcedSubtitles])
        {
            [options addObject:option];
        }
    }
    if (!group || options.count == 0)
    {
        return;
    }
    _legibleGroup = group;
    _textOptions = options;
    /* Cues come to legibleOutput:... below and VideoCaptionLayer draws them; the player draws nothing itself. */
    /* tx3g comes as native samples (parsed below): the attributed strings carry the system's caption style (it can make
       everything bold), so the cue's own styling and its end time are only in the samples. */
    AVPlayerItemLegibleOutput* legible = [[AVPlayerItemLegibleOutput alloc]
        initWithMediaSubtypesForNativeRepresentation:@[ @(kCMSubtitleFormatType_3GText) ]];
    legible.suppressesPlayerRendering = YES;
    [legible setDelegate:self queue:_queue];
    [item addOutput:legible];
    _legibleOutput = legible;
    if (group.allowsEmptySelection)
    {
        [item selectMediaOption:nil inMediaSelectionGroup:group];
    }
    [self emit:TKV_EVENT_TEXT_TRACKS a:(double)_textOptions.count b:0];
}

static void AppendEscaped(NSMutableString* out, NSString* text)
{
    NSString* escaped = [[[text stringByReplacingOccurrencesOfString:@"&" withString:@"&amp;"]
                         stringByReplacingOccurrencesOfString:@"<" withString:@"&lt;"]
                         stringByReplacingOccurrencesOfString:@">" withString:@"&gt;"];
    [out appendString:escaped];
}

static uint16_t ReadU16(const uint8_t* p)
{
    return (uint16_t)((p[0] << 8) | p[1]);
}

static uint32_t ReadU32(const uint8_t* p)
{
    return ((uint32_t)p[0] << 24) | ((uint32_t)p[1] << 16) | ((uint32_t)p[2] << 8) | p[3];
}

/* A 3GPP timed text sample (3GPP TS 26.245): a 16-bit length, the text (UTF-8, or UTF-16 after a BOM), then boxes; the
   'styl' box styles character ranges (face flags 1 bold, 2 italic, 4 underline). Returns WebVTT cue text, or nil. */
static NSString* Parse3GText(const uint8_t* data, size_t size)
{
    if (size < 2)
    {
        return nil;
    }
    size_t length = ReadU16(data);
    if (2 + length > size)
    {
        return nil;
    }
    NSString* text;
    if (length >= 2 && data[2] == 0xFE && data[3] == 0xFF)
    {
        text = [[NSString alloc] initWithBytes:data + 4 length:length - 2 encoding:NSUTF16BigEndianStringEncoding];
    }
    else
    {
        text = [[NSString alloc] initWithBytes:data + 2 length:length encoding:NSUTF8StringEncoding];
    }
    if (!text)
    {
        return nil;
    }
    /* Character offsets: code points, so work on the composed characters' UTF-32 positions. */
    NSUInteger count = 0;
    NSMutableArray<NSString*>* characters = [NSMutableArray array];
    [text enumerateSubstringsInRange:NSMakeRange(0, text.length) options:NSStringEnumerationByComposedCharacterSequences
                          usingBlock:^(NSString* sub, NSRange r, NSRange e, BOOL* stop) { [characters addObject:sub]; }];
    count = characters.count;
    uint8_t* faces = calloc(count > 0 ? count : 1, 1);
    for (size_t at = 2 + length; at + 8 <= size;)
    {
        uint32_t boxSize = ReadU32(data + at);
        if (boxSize < 8 || at + boxSize > size)
        {
            break;
        }
        if (memcmp(data + at + 4, "styl", 4) == 0 && boxSize >= 10)
        {
            uint16_t entries = ReadU16(data + at + 8);
            for (uint16_t i = 0; i < entries && at + 10 + (size_t)(i + 1) * 12 <= at + boxSize; i++)
            {
                const uint8_t* record = data + at + 10 + (size_t)i * 12;
                uint16_t start = ReadU16(record);
                uint16_t end = ReadU16(record + 2);
                uint8_t face = record[6];
                for (NSUInteger c = start; c < end && c < count; c++)
                {
                    faces[c] = face;
                }
            }
        }
        at += boxSize;
    }
    NSMutableString* out = [NSMutableString string];
    uint8_t current = 0;
    for (NSUInteger c = 0; c <= count; c++)
    {
        uint8_t face = c < count ? (faces[c] & 7) : 0;
        if (face != current)
        {
            if (current & 4) { [out appendString:@"</u>"]; }
            if (current & 2) { [out appendString:@"</i>"]; }
            if (current & 1) { [out appendString:@"</b>"]; }
            if (face & 1) { [out appendString:@"<b>"]; }
            if (face & 2) { [out appendString:@"<i>"]; }
            if (face & 4) { [out appendString:@"<u>"]; }
            current = face;
        }
        if (c < count)
        {
            NSString* character = characters[c];
            if ([character isEqualToString:@"\r"])
            {
                continue;
            }
            AppendEscaped(out, character);
        }
    }
    free(faces);
    return out;
}

- (void)legibleOutput:(AVPlayerItemLegibleOutput*)output didOutputAttributedStrings:(NSArray<NSAttributedString*>*)strings
  nativeSampleBuffers:(NSArray*)nativeSamples forItemTime:(CMTime)itemTime
{
    if (_destroyed || output != _legibleOutput || _selectedText < 0 || !_textCallback)
    {
        return;
    }
    for (id object in nativeSamples)
    {
        CMSampleBufferRef sample = (__bridge CMSampleBufferRef)object;
        CMBlockBufferRef block = CMSampleBufferGetDataBuffer(sample);
        size_t size = block ? CMBlockBufferGetDataLength(block) : 0;
        if (size < 2 || size > 65536)
        {
            continue;
        }
        uint8_t* bytes = malloc(size);
        if (!bytes || CMBlockBufferCopyDataBytes(block, 0, size, bytes) != kCMBlockBufferNoErr)
        {
            free(bytes);
            continue;
        }
        NSString* markup = Parse3GText(bytes, size);
        free(bytes);
        if (markup.length == 0)
        {
            continue;
        }
        CMTime pts = CMSampleBufferGetPresentationTimeStamp(sample);
        CMTime duration = CMSampleBufferGetDuration(sample);
        double start = CMTIME_IS_NUMERIC(pts) ? CMTimeGetSeconds(pts) : CMTimeGetSeconds(itemTime);
        double end = CMTIME_IS_NUMERIC(duration) && CMTimeGetSeconds(duration) > 0 ? start + CMTimeGetSeconds(duration) : -1;
        _textCallback(_textContext, _selectedText, start, end, TKV_TEXT_MARKUP, markup.UTF8String);
    }
    if (nativeSamples.count > 0)
    {
        return;
    }
    /* Other formats (WebVTT in HLS, CEA-608): what shows from itemTime on, with no end; the next call (empty when nothing
       shows) ends it. Plain text: the strings' styling is the system's caption style, not the cue's. */
    double start = CMTIME_IS_NUMERIC(itemTime) ? CMTimeGetSeconds(itemTime) : 0;
    NSMutableArray<NSString*>* lines = [NSMutableArray array];
    for (NSAttributedString* string in strings)
    {
        [lines addObject:string.string];
    }
    NSString* text = [lines componentsJoinedByString:@"\n"];
    _textCallback(_textContext, _selectedText, start, -1, TKV_TEXT_PLAIN, text.UTF8String);
}

- (void)setTextCallback:(tkv_text_callback)callback context:(void*)context
{
    _textCallback = callback;
    _textContext = context;
}

- (int32_t)textTrackCount
{
    return (int32_t)_textOptions.count;
}

- (int32_t)textTrack:(int32_t)index language:(char*)language size:(int32_t)languageSize label:(char*)label size:(int32_t)labelSize kind:(int32_t*)kind
{
    if (index < 0 || index >= (int32_t)_textOptions.count)
    {
        return TKV_E_INVALID_ARG;
    }
    AVMediaSelectionOption* option = _textOptions[index];
    NSString* tag = option.extendedLanguageTag ?: option.locale.languageCode ?: @"";
    NSString* name = option.displayName ?: @"";
    if (language && languageSize > 0)
    {
        snprintf(language, (size_t)languageSize, "%s", tag.UTF8String);
    }
    if (label && labelSize > 0)
    {
        snprintf(label, (size_t)labelSize, "%s", name.UTF8String);
    }
    if (kind)
    {
        BOOL captions = [option hasMediaCharacteristic:AVMediaCharacteristicTranscribesSpokenDialogForAccessibility] ||
                        [option hasMediaCharacteristic:AVMediaCharacteristicDescribesMusicAndSoundForAccessibility];
        *kind = captions ? TKV_TEXT_CAPTIONS : TKV_TEXT_SUBTITLES;
    }
    return TKV_OK;
}

- (int32_t)selectTextTrack:(int32_t)index
{
    if (index >= (int32_t)_textOptions.count || index < -1)
    {
        return TKV_E_INVALID_ARG;
    }
    if (!_item || !_legibleGroup)
    {
        return index == -1 ? TKV_OK : TKV_E_INVALID_ARG;
    }
    _selectedText = index;
    [_item selectMediaOption:index >= 0 ? _textOptions[index] : nil inMediaSelectionGroup:_legibleGroup];
    return TKV_OK;
}

- (void)observeValueForKeyPath:(NSString*)keyPath ofObject:(id)object change:(NSDictionary*)change context:(void*)context
{
    if (context != kKvoContext)
    {
        [super observeValueForKeyPath:keyPath ofObject:object change:change context:context];
        return;
    }
    __weak TKVPlayer* weakSelf = self;
    dispatch_async(_queue, ^{
        [weakSelf refresh];
    });
}

- (void)outputMediaDataWillChange:(AVPlayerItemOutput*)sender
{
    [self refresh];
}

/* ---- State polling (private queue). */

static double Seconds(CMTime time)
{
    if (!CMTIME_IS_NUMERIC(time))
    {
        return 0;
    }
    double seconds = CMTimeGetSeconds(time);
    return isfinite(seconds) && seconds > 0 ? seconds : 0;
}

- (void)refresh
{
    if (_destroyed || _failed)
    {
        return;
    }
    if (!_ready && _state == TKV_STATE_LOADING && !s_mainQueueAlive && CACurrentMediaTime() - _openedAt > kMainQueueTimeout)
    {
        NSError* error = [NSError errorWithDomain:AVFoundationErrorDomain code:AVErrorUnknown userInfo:@{
            NSLocalizedDescriptionKey: @"AVPlayer cannot load: the main dispatch queue is not serviced (run NSApplication or CFRunLoopRun on the main thread)"
        }];
        [self failWith:error kind:TKV_ERR_ABORTED];
        return;
    }
    if (!_item)
    {
        return;
    }
    AVPlayerItem* item = _item;
    if (!_ready)
    {
        if (item.status == AVPlayerItemStatusFailed)
        {
            [self failWith:item.error kind:ErrorKind(item.error)];
            return;
        }
        if (item.status != AVPlayerItemStatusReadyToPlay)
        {
            return;
        }
        _ready = YES;
        [self reportDuration];
        [self reportSize];
        [self reportSeekable];
        [self reportBuffered];
        if (_wantPlay)
        {
            [self startPlayback];
        }
        else
        {
            [self setState:TKV_STATE_PAUSED];
            [self expectFrame];
        }
        return;
    }
    if (item.status == AVPlayerItemStatusFailed)
    {
        [self failWith:item.error kind:ErrorKind(item.error)];
        return;
    }
    [self reportDuration];
    [self reportSize];
    [self reportSeekable];
    [self reportBuffered];

    AVPlayerTimeControlStatus status = _player.timeControlStatus;
    if (_wantPlay && !_endHandled)
    {
        if (status == AVPlayerTimeControlStatusPlaying)
        {
            [self setState:TKV_STATE_PLAYING];
        }
        else if (status == AVPlayerTimeControlStatusWaitingToPlayAtSpecifiedRate)
        {
            /* A seek in progress also waits; report buffering only when the player reports a reason other than seeking. */
            NSString* reason = _player.reasonForWaitingToPlay;
            if (!_seeking && reason && ![reason isEqualToString:AVPlayerWaitingWithNoItemToPlayReason])
            {
                [self setState:TKV_STATE_BUFFERING];
            }
        }
        else if (status == AVPlayerTimeControlStatusPaused && _duration > 0 && !_seeking)
        {
            /* The end notification is posted on the main queue in some hosts; notice the end here as well. */
            if (Seconds(_player.currentTime) >= _duration - 0.05)
            {
                [self handleEnd];
            }
        }
    }

    if (_framePending && !_wantPlay)
    {
        AVPlayerItemVideoOutput* output = _output;
        if (output && [output hasNewPixelBufferForItemTime:item.currentTime])
        {
            _framePending = NO;
            [self emit:TKV_EVENT_FRAME a:0 b:0];
        }
        else if (CACurrentMediaTime() - _framePendingSince > kFramePendingTimeout)
        {
            _framePending = NO;
        }
    }
}

- (void)reportDuration
{
    double duration = Seconds(_item.duration);
    if (duration == 0 && _asset)
    {
        duration = Seconds(_asset.duration);
    }
    if (fabs(duration - _duration) > 1e-6)
    {
        _duration = duration;
        [self emit:TKV_EVENT_DURATION a:duration b:0];
    }
}

- (void)reportSize
{
    int32_t width = _width;
    int32_t height = _height;
    if (width == 0 || height == 0)
    {
        CGSize size = _item.presentationSize;
        width = (int32_t)lround(size.width);
        height = (int32_t)lround(size.height);
        _width = width;
        _height = height;
    }
    if (width != _reportedWidth || height != _reportedHeight)
    {
        _reportedWidth = width;
        _reportedHeight = height;
        [self emit:TKV_EVENT_SIZE a:width b:height];
    }
}

- (void)reportSeekable
{
    int32_t seekable = _item.seekableTimeRanges.count > 0 ? 1 : 0;
    if (seekable != _seekable)
    {
        _seekable = seekable;
        [self emit:TKV_EVENT_SEEKABLE a:seekable b:0];
    }
}

- (void)reportBuffered
{
    NSArray<NSValue*>* ranges = _item.loadedTimeRanges;
    if ((_buffered == nil && ranges.count == 0) || [_buffered isEqualToArray:ranges])
    {
        return;
    }
    _buffered = [ranges copy];
    [self emit:TKV_EVENT_BUFFERED a:0 b:0];
}

- (void)expectFrame
{
    _framePending = YES;
    _framePendingSince = CACurrentMediaTime();
    [_output requestNotificationOfMediaDataChangeWithAdvanceInterval:0.05];
}

/* ---- Transport (private queue). */

- (void)startPlayback
{
    _endHandled = NO;
    _framePending = NO;
    _player.rate = (float)_rate;
    /* Like the HTML play event: report playing at once; refresh moves to buffering if the player has to wait for data. */
    [self setState:TKV_STATE_PLAYING];
}

- (void)play
{
    if (_failed || _openGeneration == 0)
    {
        return;
    }
    if (_ready && (_state == TKV_STATE_ENDED || (_duration > 0 && Seconds(_player.currentTime) >= _duration - 0.05)))
    {
        [self seekTo:0 accurate:YES];
    }
    _wantPlay = YES;
    if (_ready)
    {
        [self startPlayback];
    }
}

- (void)pause
{
    _wantPlay = NO;
    [_player pause];
    if (_ready && !_failed)
    {
        [self setState:TKV_STATE_PAUSED];
    }
}

- (void)seekTo:(double)seconds accurate:(BOOL)accurate
{
    if (!_item || _failed)
    {
        return;
    }
    if (seconds < 0)
    {
        seconds = 0;
    }
    if (_duration > 0 && seconds > _duration)
    {
        seconds = _duration;
    }
    uint64_t generation = ++_seekGeneration;
    _endHandled = NO;
    _seekTarget = seconds;
    _seeking = YES;
    if (_state == TKV_STATE_ENDED)
    {
        [self setState:TKV_STATE_PAUSED];
    }
    CMTime time = CMTimeMakeWithSeconds(seconds, 90000);
    CMTime tolerance = accurate ? kCMTimeZero : kCMTimePositiveInfinity;
    __weak TKVPlayer* weakSelf = self;
    [_player seekToTime:time toleranceBefore:tolerance toleranceAfter:tolerance completionHandler:^(BOOL finished) {
        TKVPlayer* strongSelf = weakSelf;
        if (!strongSelf)
        {
            return;
        }
        dispatch_async(strongSelf->_queue, ^{
            [strongSelf seekDone:generation finished:finished];
        });
    }];
}

- (void)seekDone:(uint64_t)generation finished:(BOOL)finished
{
    if (_destroyed || generation != _seekGeneration)
    {
        return;
    }
    _seeking = NO;
    if (!finished)
    {
        return;
    }
    [self emit:TKV_EVENT_POSITION a:Seconds(_player.currentTime) b:0];
    if (!_wantPlay)
    {
        [self expectFrame];
    }
    [self refresh];
}

- (void)handleEnd
{
    if (_endHandled || !_ready || _failed)
    {
        return;
    }
    if (_loop && _wantPlay)
    {
        _endHandled = YES;
        uint64_t generation = ++_seekGeneration;
        _seekTarget = 0;
        _seeking = YES;
        __weak TKVPlayer* weakSelf = self;
        [_player seekToTime:kCMTimeZero toleranceBefore:kCMTimeZero toleranceAfter:kCMTimeZero completionHandler:^(BOOL finished) {
            TKVPlayer* strongSelf = weakSelf;
            if (!strongSelf)
            {
                return;
            }
            dispatch_async(strongSelf->_queue, ^{
                if (strongSelf->_destroyed || generation != strongSelf->_seekGeneration)
                {
                    return;
                }
                strongSelf->_seeking = NO;
                strongSelf->_endHandled = NO;
                if (strongSelf->_wantPlay)
                {
                    strongSelf->_player.rate = (float)strongSelf->_rate;
                }
                [strongSelf emit:TKV_EVENT_POSITION a:0 b:0];
            });
        }];
        return;
    }
    _endHandled = YES;
    _wantPlay = NO;
    [_player pause];
    [self emit:TKV_EVENT_POSITION a:_duration b:0];
    [self setState:TKV_STATE_ENDED];
    [self emit:TKV_EVENT_ENDED a:0 b:0];
}

- (int32_t)setRate:(double)rate
{
    if (!(rate > 0) || rate < 0.25 || rate > 4)
    {
        return TKV_E_INVALID_ARG;
    }
    if (_item && rate > 2 && !_item.canPlayFastForward)
    {
        return TKV_E_UNSUPPORTED;
    }
    if (_item && rate < 1 && !_item.canPlaySlowForward)
    {
        return TKV_E_UNSUPPORTED;
    }
    _rate = rate;
    if (_wantPlay && _ready)
    {
        _player.rate = (float)rate;
    }
    return TKV_OK;
}

- (void)destroy
{
    [self sync:^{
        self->_destroyed = YES;
        self->_callback = NULL;
        [self detachItem];
        [self->_player removeObserver:self forKeyPath:@"timeControlStatus" context:kKvoContext];
        dispatch_source_cancel(self->_timer);
    }];
    os_unfair_lock_lock(&_frameLock);
    [self releaseHeldLocked];
    /* The compositor keeps its own references to imported IOSurfaces; the pool buffers can go. */
    [_pending removeAllObjects];
    [_images removeAllObjects];
    if (_lastBuffer)
    {
        CVPixelBufferRelease(_lastBuffer);
        _lastBuffer = NULL;
    }
    _frameOutput = nil;
    os_unfair_lock_unlock(&_frameLock);
}

/* ---- Frames (caller's thread, under _frameLock). */

/* Returns the held buffer to the pool, or parks it until the compositor's signal value is reached when the frame was
   presented with a timeline semaphore (the pool would otherwise let the decoder overwrite an IOSurface still being read). */
- (void)releaseHeldLocked
{
    if (!_held)
    {
        return;
    }
    if (_heldLocked)
    {
        CVPixelBufferUnlockBaseAddress(_held, kCVPixelBufferLock_ReadOnly);
    }
    if (_heldSync && _heldSync->event.signaledValue < _heldSyncValue)
    {
        TKVPendingBuffer* pending = [[TKVPendingBuffer alloc] init];
        pending->buffer = _held;
        pending->event = _heldSync->event;
        pending->value = _heldSyncValue;
        pending->since = CACurrentMediaTime();
        [_pending addObject:pending];
    }
    else
    {
        CVPixelBufferRelease(_held);
    }
    _held = NULL;
    _heldLocked = NO;
    _heldToken = 0;
    _heldSync = nil;
    _heldSyncValue = 0;
    _heldImageId = 0;
}

/* Releases parked buffers whose signal value was reached, and any older than kPendingTimeout (a frame the compositor
   dropped never signals). */
- (void)reapPendingLocked
{
    if (_pending.count == 0)
    {
        return;
    }
    double now = CACurrentMediaTime();
    NSIndexSet* done = [_pending indexesOfObjectsPassingTest:^BOOL(TKVPendingBuffer* item, NSUInteger index, BOOL* stop) {
        return item->event.signaledValue >= item->value || now - item->since > kPendingTimeout;
    }];
    [_pending removeObjectsAtIndexes:done];
}

/* Scales the locked CPU frame in frame (BGRA) into _scaled at width × height and points frame at it. NO when memory runs
   out or vImage fails; the frame is then handed out at its natural size. */
- (BOOL)scaleLocked:(tkv_frame*)frame width:(int32_t)width height:(int32_t)height
{
    size_t rowBytes = (size_t)width * 4;
    size_t needed = rowBytes * (size_t)height;
    if (needed > _scaledCapacity)
    {
        uint8_t* grown = (uint8_t*)realloc(_scaled, needed);
        if (!grown)
        {
            return NO;
        }
        _scaled = grown;
        _scaledCapacity = needed;
    }
    vImage_Buffer source = { (void*)frame->pixels, (vImagePixelCount)frame->height, (vImagePixelCount)frame->width, (size_t)frame->row_bytes };
    vImage_Buffer destination = { _scaled, (vImagePixelCount)height, (vImagePixelCount)width, rowBytes };
    /* The channel order does not matter to the scaler, so the ARGB8888 entry point scales BGRA as is. */
    vImage_Error scratch = vImageScale_ARGB8888(&source, &destination, NULL, kvImageGetTempBufferSize);
    if (scratch < 0)
    {
        return NO;
    }
    if ((size_t)scratch > _scaleScratchCapacity)
    {
        void* grown = realloc(_scaleScratch, (size_t)scratch);
        if (!grown)
        {
            return NO;
        }
        _scaleScratch = grown;
        _scaleScratchCapacity = (size_t)scratch;
    }
    if (vImageScale_ARGB8888(&source, &destination, _scaleScratch, kvImageNoFlags) != kvImageNoError)
    {
        return NO;
    }
    frame->pixels = _scaled;
    frame->row_bytes = (int32_t)rowBytes;
    frame->width = width;
    frame->height = height;
    return YES;
}

- (int32_t)acquire:(const tkv_frame_request*)request frame:(tkv_frame*)frame
{
    BOOL wantGpu = (request->accept & TKV_FRAME_GPU) && request->gpu_handle_type == TKV_GPU_IOSURFACE;
    BOOL wantCpu = (request->accept & TKV_FRAME_CPU_BGRA) != 0;
    if (!wantGpu && !wantCpu)
    {
        return TKV_E_UNSUPPORTED;
    }
    os_unfair_lock_lock(&_frameLock);
    [self releaseHeldLocked];
    [self reapPendingLocked];
    AVPlayerItemVideoOutput* output = _frameOutput;
    if (!output)
    {
        os_unfair_lock_unlock(&_frameLock);
        return TKV_E_NO_FRAME;
    }
    CFTimeInterval hostTime = request->host_time_ns > 0 ? (double)request->host_time_ns / 1e9 : CACurrentMediaTime();
    CMTime itemTime = [output itemTimeForHostTime:hostTime];
    CMTime shown = kCMTimeInvalid;
    CVPixelBufferRef buffer = NULL;
    BOOL redelivered = NO;
    if (CMTIME_IS_VALID(itemTime) && [output hasNewPixelBufferForItemTime:itemTime])
    {
        buffer = [output copyPixelBufferForItemTime:itemTime itemTimeForDisplay:&shown];
        if (buffer && !CMTIME_IS_NUMERIC(shown))
        {
            shown = itemTime;
        }
    }
    if (!buffer && _redeliver && _lastBuffer)
    {
        buffer = CVPixelBufferRetain(_lastBuffer);
        shown = _lastTime;
        redelivered = YES;
    }
    if (!buffer)
    {
        os_unfair_lock_unlock(&_frameLock);
        return TKV_E_NO_FRAME;
    }
    memset(frame, 0, sizeof(*frame));
    frame->width = (int32_t)CVPixelBufferGetWidth(buffer);
    frame->height = (int32_t)CVPixelBufferGetHeight(buffer);
    frame->rotation = _rotation;
    frame->time = CMTIME_IS_NUMERIC(shown) ? CMTimeGetSeconds(shown) : 0;
    IOSurfaceRef surface = wantGpu ? CVPixelBufferGetIOSurface(buffer) : NULL;
    if (surface)
    {
        int64_t imageId = (int64_t)IOSurfaceGetID(surface);
        TKVImageSync* sync = _images[@(imageId)];
        if (sync && sync->event.signaledValue < sync->lastSignal)
        {
            if (redelivered)
            {
                /* The compositor is still reading this very image; try again on the next frame. */
                CVPixelBufferRelease(buffer);
                os_unfair_lock_unlock(&_frameLock);
                return TKV_E_NO_FRAME;
            }
            /* A parked buffer of this IOSurface timed out: start a fresh timeline so the values only move forward. */
            [_images removeObjectForKey:@(imageId)];
        }
        frame->kind = TKV_FRAME_GPU;
        frame->gpu_handle_type = TKV_GPU_IOSURFACE;
        frame->gpu_handle = (void*)surface;
        frame->gpu_image_id = imageId;
        _heldImageId = imageId;
        _heldLocked = NO;
    }
    else if (wantCpu)
    {
        if (CVPixelBufferLockBaseAddress(buffer, kCVPixelBufferLock_ReadOnly) != kCVReturnSuccess)
        {
            CVPixelBufferRelease(buffer);
            os_unfair_lock_unlock(&_frameLock);
            [self setError:@"CVPixelBufferLockBaseAddress failed"];
            return TKV_E_FAIL;
        }
        frame->kind = TKV_FRAME_CPU_BGRA;
        frame->pixels = (const uint8_t*)CVPixelBufferGetBaseAddress(buffer);
        frame->row_bytes = (int32_t)CVPixelBufferGetBytesPerRow(buffer);
        _heldLocked = YES;
        int32_t w, h;
        tkv_fit_size(frame->width, frame->height, request->target_width, request->target_height, &w, &h);
        if ((w != frame->width || h != frame->height) && [self scaleLocked:frame width:w height:h])
        {
            /* The scaled copy is what is handed out; the decoder's buffer is no longer read. */
            CVPixelBufferUnlockBaseAddress(buffer, kCVPixelBufferLock_ReadOnly);
            _heldLocked = NO;
        }
    }
    else
    {
        CVPixelBufferRelease(buffer);
        os_unfair_lock_unlock(&_frameLock);
        return TKV_E_UNSUPPORTED;
    }
    if (_lastBuffer != buffer)
    {
        if (_lastBuffer)
        {
            CVPixelBufferRelease(_lastBuffer);
        }
        _lastBuffer = CVPixelBufferRetain(buffer);
    }
    _lastTime = CMTIME_IS_NUMERIC(shown) ? shown : kCMTimeZero;
    _redeliver = NO;
    _held = buffer;
    _heldToken = ++_nextToken;
    frame->token = _heldToken;
    os_unfair_lock_unlock(&_frameLock);
    return TKV_OK;
}

- (void)releaseFrame:(int64_t)token
{
    os_unfair_lock_lock(&_frameLock);
    if (_held && token == _heldToken)
    {
        [self releaseHeldLocked];
    }
    os_unfair_lock_unlock(&_frameLock);
}

- (void)redeliver
{
    os_unfair_lock_lock(&_frameLock);
    _redeliver = YES;
    os_unfair_lock_unlock(&_frameLock);
}

- (int32_t)timelineSync:(int64_t)token semaphore:(void**)semaphore type:(int32_t*)type wait:(uint64_t*)waitValue signal:(uint64_t*)signalValue
{
    os_unfair_lock_lock(&_frameLock);
    if (!_held || token != _heldToken || _heldImageId == 0)
    {
        os_unfair_lock_unlock(&_frameLock);
        return TKV_E_INVALID_ARG;
    }
    if (!_device)
    {
        os_unfair_lock_unlock(&_frameLock);
        [self setError:@"no Metal device for the timeline semaphore"];
        return TKV_E_UNSUPPORTED;
    }
    TKVImageSync* sync = _images[@(_heldImageId)];
    if (!sync)
    {
        sync = [[TKVImageSync alloc] init];
        sync->event = [_device newSharedEvent];
        if (!sync->event)
        {
            os_unfair_lock_unlock(&_frameLock);
            [self setError:@"MTLDevice newSharedEvent failed"];
            return TKV_E_FAIL;
        }
        sync->lastSignal = 0;
        _images[@(_heldImageId)] = sync;
    }
    if (_heldSync == sync)
    {
        /* Asked twice for the same frame: hand out the same values. */
        *semaphore = (__bridge void*)sync->event;
        *type = TKV_SEM_METAL_SHARED_EVENT;
        *waitValue = _heldSyncValue - 1;
        *signalValue = _heldSyncValue;
        os_unfair_lock_unlock(&_frameLock);
        return TKV_OK;
    }
    /* acquire made sure the previous signal value was reached, so the timeline only moves forward. The pixel buffer is
       complete once copied from the video output, so the producer side signals the wait value right away. */
    uint64_t wait = sync->lastSignal + 1;
    uint64_t signal = sync->lastSignal + 2;
    sync->event.signaledValue = wait;
    sync->lastSignal = signal;
    _heldSync = sync;
    _heldSyncValue = signal;
    *semaphore = (__bridge void*)sync->event;
    *type = TKV_SEM_METAL_SHARED_EVENT;
    *waitValue = wait;
    *signalValue = signal;
    os_unfair_lock_unlock(&_frameLock);
    return TKV_OK;
}

@end

/* ---- C ABI. */

static TKVPlayer* P(tkv_player* player)
{
    return (__bridge TKVPlayer*)(void*)player;
}

TKV_API int32_t tkv_version(void)
{
    return TKV_ABI_VERSION;
}

TKV_API int32_t tkv_initialize(void)
{
    if (!NSClassFromString(@"AVPlayer"))
    {
        return TKV_E_UNAVAILABLE;
    }
    ProbeMainQueue();
    return TKV_OK;
}

TKV_API int32_t tkv_frame_kinds(void)
{
    return TKV_FRAME_CPU_BGRA | TKV_FRAME_GPU;
}

TKV_API int32_t tkv_gpu_handle_type(void)
{
    return TKV_GPU_IOSURFACE;
}

TKV_API int32_t tkv_can_play(const char* media_type)
{
    if (!media_type || !*media_type)
    {
        return 0;
    }
    @autoreleasepool
    {
        NSString* type = [[NSString stringWithUTF8String:media_type] stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceCharacterSet];
        if (!type.length)
        {
            return 0;
        }
        NSString* bare = [[type componentsSeparatedByString:@";"].firstObject stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceCharacterSet].lowercaseString;
        BOOL hasCodecs = [type rangeOfString:@"codecs" options:NSCaseInsensitiveSearch].location != NSNotFound;
        if (![AVURLAsset isPlayableExtendedMIMEType:type])
        {
            return 0;
        }
        if (![[AVURLAsset audiovisualMIMETypes] containsObject:bare])
        {
            return 0;
        }
        return hasCodecs ? 2 : 1;
    }
}

TKV_API int32_t tkv_player_create(tkv_event_callback callback, void* context, const uint8_t* adapter_luid, tkv_player** player)
{
    if (!player)
    {
        return TKV_E_INVALID_ARG;
    }
    @autoreleasepool
    {
        TKVPlayer* instance = [[TKVPlayer alloc] initWithCallback:callback context:context luid:adapter_luid];
        if (!instance)
        {
            return TKV_E_FAIL;
        }
        *player = (tkv_player*)(__bridge_retained void*)instance;
        return TKV_OK;
    }
}

TKV_API void tkv_player_destroy(tkv_player* player)
{
    if (!player)
    {
        return;
    }
    @autoreleasepool
    {
        TKVPlayer* instance = (__bridge_transfer TKVPlayer*)(void*)player;
        [instance destroy];
        instance = nil;
    }
}

TKV_API int32_t tkv_player_open(tkv_player* player, const char* url)
{
    if (!player || !url)
    {
        return TKV_E_INVALID_ARG;
    }
    @autoreleasepool
    {
        NSURL* nsurl = [NSURL URLWithString:[NSString stringWithUTF8String:url]];
        if (!nsurl || !nsurl.scheme)
        {
            return TKV_E_INVALID_ARG;
        }
        __block int32_t result = TKV_OK;
        TKVPlayer* instance = P(player);
        [instance sync:^{
            result = [instance open:nsurl];
        }];
        return result;
    }
}

TKV_API int32_t tkv_player_play(tkv_player* player)
{
    if (!player)
    {
        return TKV_E_INVALID_ARG;
    }
    @autoreleasepool
    {
        TKVPlayer* instance = P(player);
        [instance sync:^{
            [instance play];
        }];
        return TKV_OK;
    }
}

TKV_API int32_t tkv_player_pause(tkv_player* player)
{
    if (!player)
    {
        return TKV_E_INVALID_ARG;
    }
    @autoreleasepool
    {
        TKVPlayer* instance = P(player);
        [instance sync:^{
            [instance pause];
        }];
        return TKV_OK;
    }
}

TKV_API int32_t tkv_player_seek(tkv_player* player, double seconds, int32_t accurate)
{
    if (!player || !isfinite(seconds))
    {
        return TKV_E_INVALID_ARG;
    }
    @autoreleasepool
    {
        TKVPlayer* instance = P(player);
        [instance sync:^{
            [instance seekTo:seconds accurate:accurate != 0];
        }];
        return TKV_OK;
    }
}

TKV_API double tkv_player_position(tkv_player* player)
{
    if (!player)
    {
        return 0;
    }
    TKVPlayer* instance = P(player);
    if (instance->_seeking)
    {
        return instance->_seekTarget;
    }
    double seconds = Seconds(instance->_player.currentTime);
    return seconds;
}

TKV_API int32_t tkv_player_set_volume(tkv_player* player, double volume)
{
    if (!player || !isfinite(volume))
    {
        return TKV_E_INVALID_ARG;
    }
    float clamped = (float)fmin(1.0, fmax(0.0, volume));
    TKVPlayer* instance = P(player);
    [instance sync:^{
        instance->_volume = clamped;
        instance->_player.volume = clamped;
    }];
    return TKV_OK;
}

TKV_API int32_t tkv_player_set_rate(tkv_player* player, double rate)
{
    if (!player)
    {
        return TKV_E_INVALID_ARG;
    }
    __block int32_t result = TKV_OK;
    TKVPlayer* instance = P(player);
    [instance sync:^{
        result = [instance setRate:rate];
    }];
    return result;
}

TKV_API int32_t tkv_player_set_loop(tkv_player* player, int32_t loop)
{
    if (!player)
    {
        return TKV_E_INVALID_ARG;
    }
    TKVPlayer* instance = P(player);
    [instance sync:^{
        instance->_loop = loop != 0;
    }];
    return TKV_OK;
}

TKV_API int32_t tkv_player_buffered(tkv_player* player, double* ranges, int32_t capacity)
{
    if (!player || capacity < 0 || (capacity > 0 && !ranges))
    {
        return TKV_E_INVALID_ARG;
    }
    __block int32_t count = 0;
    TKVPlayer* instance = P(player);
    [instance sync:^{
        for (NSValue* value in instance->_item.loadedTimeRanges)
        {
            if (count >= capacity)
            {
                break;
            }
            CMTimeRange range = value.CMTimeRangeValue;
            double start = Seconds(range.start);
            ranges[2 * count] = start;
            ranges[2 * count + 1] = start + Seconds(range.duration);
            count++;
        }
    }];
    return count;
}

TKV_API int32_t tkv_player_acquire_frame(tkv_player* player, const tkv_frame_request* request, tkv_frame* frame)
{
    if (!player || !request || !frame)
    {
        return TKV_E_INVALID_ARG;
    }
    @autoreleasepool
    {
        return [P(player) acquire:request frame:frame];
    }
}

TKV_API void tkv_player_release_frame(tkv_player* player, int64_t token)
{
    if (!player)
    {
        return;
    }
    [P(player) releaseFrame:token];
}

TKV_API const char* tkv_player_last_error(tkv_player* player)
{
    if (!player)
    {
        return "";
    }
    return [P(player) lastError];
}

TKV_API int32_t tkv_player_timeline_sync(tkv_player* player, int64_t token, void** semaphore, int32_t* semaphore_type, uint64_t* wait_value,
    uint64_t* signal_value)
{
    if (!player || !semaphore || !semaphore_type || !wait_value || !signal_value)
    {
        return TKV_E_INVALID_ARG;
    }
    @autoreleasepool
    {
        return [P(player) timelineSync:token semaphore:semaphore type:semaphore_type wait:wait_value signal:signal_value];
    }
}

TKV_API int32_t tkv_player_redeliver_frame(tkv_player* player)
{
    if (!player)
    {
        return TKV_E_INVALID_ARG;
    }
    [P(player) redeliver];
    return TKV_OK;
}

TKV_API int32_t tkv_player_set_text_callback(tkv_player* player, tkv_text_callback callback, void* context)
{
    if (!player)
    {
        return TKV_E_INVALID_ARG;
    }
    TKVPlayer* instance = P(player);
    [instance sync:^{
        [instance setTextCallback:callback context:context];
    }];
    return TKV_OK;
}

TKV_API int32_t tkv_player_text_track_count(tkv_player* player)
{
    if (!player)
    {
        return TKV_E_INVALID_ARG;
    }
    __block int32_t count = 0;
    TKVPlayer* instance = P(player);
    [instance sync:^{
        count = [instance textTrackCount];
    }];
    return count;
}

TKV_API int32_t tkv_player_text_track_info(tkv_player* player, int32_t index, char* language, int32_t language_size,
                                           char* label, int32_t label_size, int32_t* kind)
{
    if (!player)
    {
        return TKV_E_INVALID_ARG;
    }
    @autoreleasepool
    {
        __block int32_t result = TKV_OK;
        TKVPlayer* instance = P(player);
        [instance sync:^{
            result = [instance textTrack:index language:language size:language_size label:label size:label_size kind:kind];
        }];
        return result;
    }
}

TKV_API int32_t tkv_player_select_text_track(tkv_player* player, int32_t index)
{
    if (!player)
    {
        return TKV_E_INVALID_ARG;
    }
    __block int32_t result = TKV_OK;
    TKVPlayer* instance = P(player);
    [instance sync:^{
        result = [instance selectTextTrack:index];
    }];
    return result;
}
