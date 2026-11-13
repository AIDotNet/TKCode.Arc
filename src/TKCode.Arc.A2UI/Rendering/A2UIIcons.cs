using TKCode.Arc.Icons.Lucide;

namespace TKCode.Arc.A2UI;

/// <summary>The basic catalog's icon names (Material style) mapped to Lucide icons.</summary>
public static class A2UIIcons
{
    /// <summary>The Lucide icon for a basic catalog icon name; false for an unknown name.</summary>
    public static bool TryMap(string name, out LucideIconKind kind)
    {
        kind = name switch
        {
            "accountCircle" => LucideIconKind.CircleUser,
            "add" => LucideIconKind.Plus,
            "arrowBack" => LucideIconKind.ArrowLeft,
            "arrowForward" => LucideIconKind.ArrowRight,
            "attachFile" => LucideIconKind.Paperclip,
            "calendarToday" => LucideIconKind.Calendar,
            "call" => LucideIconKind.PhoneCall,
            "camera" => LucideIconKind.Camera,
            "check" => LucideIconKind.Check,
            "close" => LucideIconKind.X,
            "delete" => LucideIconKind.Trash2,
            "download" => LucideIconKind.Download,
            "edit" => LucideIconKind.Pencil,
            "event" => LucideIconKind.CalendarDays,
            "error" => LucideIconKind.CircleAlert,
            "fastForward" => LucideIconKind.FastForward,
            "favorite" => LucideIconKind.Heart,
            "favoriteOff" => LucideIconKind.HeartOff,
            "folder" => LucideIconKind.Folder,
            "help" => LucideIconKind.CircleQuestionMark,
            "home" => LucideIconKind.House,
            "info" => LucideIconKind.Info,
            "locationOn" => LucideIconKind.MapPin,
            "lock" => LucideIconKind.Lock,
            "lockOpen" => LucideIconKind.LockOpen,
            "mail" => LucideIconKind.Mail,
            "menu" => LucideIconKind.Menu,
            "moreVert" => LucideIconKind.EllipsisVertical,
            "moreHoriz" => LucideIconKind.Ellipsis,
            "notificationsOff" => LucideIconKind.BellOff,
            "notifications" => LucideIconKind.Bell,
            "pause" => LucideIconKind.Pause,
            "payment" => LucideIconKind.CreditCard,
            "person" => LucideIconKind.User,
            "phone" => LucideIconKind.Phone,
            "photo" => LucideIconKind.Image,
            "play" => LucideIconKind.Play,
            "print" => LucideIconKind.Printer,
            "refresh" => LucideIconKind.RefreshCw,
            "rewind" => LucideIconKind.Rewind,
            "search" => LucideIconKind.Search,
            "send" => LucideIconKind.Send,
            "settings" => LucideIconKind.Settings,
            "share" => LucideIconKind.Share2,
            "shoppingCart" => LucideIconKind.ShoppingCart,
            "skipNext" => LucideIconKind.SkipForward,
            "skipPrevious" => LucideIconKind.SkipBack,
            "star" => LucideIconKind.Star,
            "starHalf" => LucideIconKind.StarHalf,
            "starOff" => LucideIconKind.StarOff,
            "stop" => LucideIconKind.Square,
            "upload" => LucideIconKind.Upload,
            "visibility" => LucideIconKind.Eye,
            "visibilityOff" => LucideIconKind.EyeOff,
            "volumeDown" => LucideIconKind.Volume1,
            "volumeMute" => LucideIconKind.VolumeX,
            "volumeOff" => LucideIconKind.VolumeOff,
            "volumeUp" => LucideIconKind.Volume2,
            "warning" => LucideIconKind.TriangleAlert,
            _ => (LucideIconKind)(-1)
        };
        return (int)kind >= 0;
    }
}
