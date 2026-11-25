# Third-party notices

TKCode.Arc ports the design and behavior of the free components of Arc and uses the
assets and libraries listed below.

## Arc (uiarc.dev) — free components and blocks

TKCode.Arc reimplements the free (MIT) components of Arc for Avalonia, and `TKCode.Arc.Blocks` ports
Arc's free (MIT) blocks (Page header, Command palette, Notification center, Empty states, Site header, Site footer, CTA section, Newsletter signup, OTP input, Plan comparison and the Email code, Centered and Sign up login blocks). The code,
styles, text and assets of Arc Pro components and blocks are not used, copied or reconstructed. A few Pro
counterparts (the data grid, the Sidebar and Search results blocks, the AI blocks, the Showpieces blocks, the Projects blocks and the
five AI explainers and the five AI components (agent status, tool call, agent plan, agent timeline, context
window) and the six surfaces (sheet stack, wallet stack, control center, holo card, card to article, link unfurl)
and the three cursor components (dot grid, gravity well, ink fluid)
and the three audio components (voice orb, now playing, voice recorder)
and the five loaders (stretch refresh, morph loader, skeleton morph, black hole, charge session)
and the six docks and tabs components (share sheet, action morph, morph nav, tool dock, liquid tab bar, orbit menu) in
`TKCode.Arc.Special`, and the Split and Immersive login blocks, Onboarding illustration and Multi-step form and the seven Pricing blocks (pricing calculator, usage
pricing, cart drawer, checkout summary, live checkout, seat manager, credit top-up) and the Import wizard block in
`TKCode.Arc.Blocks`) are
original TKCode designs that take the behavior and look Arc publishes as a design reference; their code, copy,
sample data and images are written from scratch (the Showpieces' land outlines come from Natural Earth, below).

Other components and blocks in `TKCode.Arc.Special` and `TKCode.Arc.Blocks` share a name with an Arc Pro
item but are independent TKCode designs: they were built from the general idea the name describes, without
looking at the Arc Pro pages, docs, previews, source or assets, so their look and behavior are our own.

```
MIT License

Copyright (c) 2026 Elia Kuratli

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Lucide icons

Icon geometry in `TKCode.Arc.Core` (Glyphs) and `TKCode.Arc.Icons.Lucide` is generated from
lucide-static.

```
ISC License

Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2026 as part of Feather (MIT).
All other copyright (c) for Lucide are held by Lucide Contributors 2026.

Permission to use, copy, modify, and/or distribute this software for any purpose with or
without fee is hereby granted, provided that the above copyright notice and this permission
notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS
SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL
THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY
DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF
CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE
OR PERFORMANCE OF THIS SOFTWARE.
```

## Unicode emoji data — `TKCode.Arc.Special` (EmojiPicker)

`EmojiPicker` embeds a table built by `tools/emoji/generate_emoji.py` from Unicode `emoji-test.txt` 16.0
(https://unicode.org/Public/emoji/16.0/emoji-test.txt) and the English CLDR annotations
(`cldr-json`, `cldr-annotations-full/annotations/en/annotations.json`). Both are Copyright © Unicode, Inc.,
distributed under the Unicode License v3 (https://www.unicode.org/license.txt).

## Natural Earth — land outlines

`TKCode.Arc.Blocks` embeds a 0.5° land mask made from **Natural Earth** 1:110m Physical Vectors, Land
(`ne_110m_land`), for the `RevenueGlobe` block's dotted continents (`tools/geo/generate_land_mask.py` rasterizes the
polygons; the mask is `Blocks/Showpieces/RevenueGlobe/Resources/land-mask.bin`, 32 KB). Natural Earth data is in
the public domain. Made with Natural Earth. Free vector and raster map data at https://www.naturalearthdata.com.

## Fonts

- Inter (SIL Open Font License 1.1), used through the `Avalonia.Fonts.Inter` package.
- Geist and Geist Mono (SIL Open Font License 1.1, Copyright (c) 2023 Vercel, in collaboration with basement.studio), bundled in the optional `TKCode.Arc.Fonts` package; the full license text ships as `licenses/Geist-OFL.txt` (source: `src/TKCode.Arc.Fonts/Assets/OFL.txt`).
- Silkscreen (SIL Open Font License 1.1, Copyright 2001 The Silkscreen Project Authors) and VT323 (SIL Open Font License 1.1, Copyright 2011 The VT323 Project Authors), bundled in `TKCode.Arc.Fonts` for the pixel theme as `fonts:ArcPixel`; the license texts ship as `licenses/Silkscreen-OFL.txt` and `licenses/VT323-OFL.txt` (sources: `src/TKCode.Arc.Fonts/Assets/`).
- Noto Sans SC (SIL Open Font License 1.1, © 2014-2021 Adobe), subset to the characters of the gallery (`tools/fonts/subset-noto-sans-sc.py` in the [ArcGallery](https://github.com/AIDotNet/ArcGallery) repository) as `NotoSansSC-Subset.otf` in the gallery's WebAssembly host, which has no system fonts for Chinese text; the license text ships beside it as `wwwroot/fonts/OFL.txt`.

## StreamMark — Markdown rendering and code highlighting

StreamMark is part of this repository (`src/TKCode.Arc.StreamMark*`, moved in from its own repository with its MIT
license below). `TKCode.Arc.Markdown` references `TKCode.Arc.StreamMark` (which uses a native incremental Markdown
parser with no Markdig dependency since `1.3.0-preview.4`), and `TKCode.Arc.Highlighting` references
`TKCode.Arc.StreamMark.TextMate` (and through it TextMateSharp, its grammars and Oniguruma). They render the `Markdown`
control and tokenize the code of `CodeBlock`; `TKCode.Arc` itself references none of them. The optional
`TKCode.Arc.StreamMark.RaTeX` and `TKCode.Arc.StreamMark.Mermaid` packages are covered below.

- **remend (vercel/streamdown).** `src/TKCode.Arc.StreamMark/Core/Remend/` is a C# port of `packages/remend` from
  https://github.com/vercel/streamdown, and the remend fixtures under `tests/TKCode.Arc.StreamMark.Tests/Fixtures/remend/`
  are derived from its test suite (generated by `tools/streammark/remend-fixtures/`). Copyright 2023 Vercel, Inc.
  Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0). Modifications: ported to
  C#, sentinel URL scheme changed from `streamdown:` to `streammark:`.
- **LiveMarkdown.Avalonia.** Backslash math delimiter handling (`\(...\)` / `\[...\]`) in
  `src/TKCode.Arc.StreamMark/Core/Parsing/InlineParser.cs` follows the approach of LiveMarkdown.Avalonia
  (https://github.com/DearVa/LiveMarkdown.Avalonia), licensed under the Apache License, Version 2.0.

```
MIT License

Copyright (c) 2026 StreamMark contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### TextMateSharp and TextMateSharp.Grammars

TextMate tokenization (dependency of `TKCode.Arc.StreamMark.TextMate`).

```
MIT License

Copyright (c) 2021 Daniel Peñalba

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

TextMateSharp.Grammars embeds TextMate grammars, language configurations and color themes, most of them taken
from Visual Studio Code (MIT License, Copyright (c) Microsoft Corporation) and the grammar repositories it
bundles (MIT, TextMate bundle license, Apache-2.0, BSD and GitHub licenses). The `cgmanifest.json` files embedded
in the package record the source and license of each grammar. TKCode.Arc tokenizes with VS Code's Light+ theme
and paints the result in its own palette.

### Onigwrap and Oniguruma

TextMateSharp runs its regular expressions on the native Oniguruma library through Onigwrap (native libraries per
runtime identifier). Onigwrap: MIT License, Copyright (c) 2024 Aikawa Yataro, derived from fluentCODE/onigwrap
(MIT License, Copyright (c) 2015 Fluent Solutions). The Gallery's WebAssembly host (the [ArcGallery](https://github.com/AIDotNet/ArcGallery) repository) links
`gallery/TKCode.Arc.Gallery.Browser/native/onigwrap/<version>/st/libonigwrap.a`, compiled from Onigwrap 1.0.11 and
Oniguruma 6.9.10 by `tools/onigwrap/build-wasm.sh` (the package has no build for the Emscripten of .NET 11). Oniguruma:

```
Copyright (c) 2002-2021  K.Kosako  <kkosako0@gmail.com>
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
1. Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.
```

### RaTeX and the KaTeX fonts (`TKCode.Arc.StreamMark.RaTeX`)

`TKCode.Arc.StreamMark.RaTeX` ships `libratex_ffi`, built from RaTeX (https://github.com/erweixin/RaTeX, MIT License) at
the commit pinned in `native/ratex/ratex-upstream.json`, and embeds the
KaTeX fonts that RaTeX distributes (SIL Open Font License 1.1). The font license and notice ship in the package under
`licenses/` (sources: `src/TKCode.Arc.StreamMark.RaTeX/Fonts/SIL-OFL-1.1.txt` and `KaTeX-fonts-NOTICE.txt`).

### Mermaider and Svg.Skia (`TKCode.Arc.StreamMark.Mermaid`)

`TKCode.Arc.StreamMark.Mermaid` references Mermaider and Svg.Skia (with Svg.Model, Svg.Custom, ShimSkiaSharp,
Svg.SceneGraph and Svg.Animation) as NuGet packages, under their MIT licenses. Neither is referenced by TKCode.Arc's
other packages.

## miniaudio — desktop audio playback

`TKCode.Arc.Audio.Native` compiles miniaudio 0.11.26 (David Reid, https://github.com/mackron/miniaudio) into its native
library. miniaudio is offered as public domain (Unlicense) or MIT No Attribution; TKCode.Arc uses it under MIT-0:

```
Copyright 2026 David Reid

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## PDFium — `TKCode.Arc.Pdf.Native`

`TKCode.Arc.Pdf.Native` ships PDFium's native library and the components it bundles. Only that package carries them, so their notices and license texts travel with it and not with every package: see [`src/TKCode.Arc.Pdf.Native/PDFIUM-NOTICES.md`](src/TKCode.Arc.Pdf.Native/PDFIUM-NOTICES.md) (`PDFIUM-NOTICES.md` and `licenses/` in the package).

## pdf.js — `TKCode.Arc.Pdf.Browser`

`TKCode.Arc.Pdf.Browser` ships pdf.js and the data and decoders it needs. Only that package carries them, so their notices and license texts travel with it and not with every package: see [`src/TKCode.Arc.Pdf.Browser/PDFJS-NOTICES.md`](src/TKCode.Arc.Pdf.Browser/PDFJS-NOTICES.md) (`PDFJS-NOTICES.md` and the files under `staticwebassets/pdfjs/` in the package).

## Visual Studio Code and the Monaco editor — `TKCode.Arc.Editor`

`TKCode.Arc.Editor` is a code editor engine for .NET modelled on the Monaco editor, the editor of Visual Studio Code
(https://github.com/microsoft/vscode, `src/vs/editor`). Its layering, object model and behavior follow Monaco's, and
parts of it are ported from the TypeScript source: the position, range and selection types, the piece tree text buffer
and its red-black tree, the text model with its edit stack, events and options, the decoration interval tree, find, the
indentation guesser, string helpers from `src/vs/base/common`, the undo-redo service from `src/vs/platform/undoRedo`, the cursor and view model, context keys and `when`
expressions from `src/vs/platform/contextkey`, key codes, keybindings and the keybinding resolver from
`src/vs/base/common` and `src/vs/platform/keybinding`, the command registry from `src/vs/platform/commands`, the core
editor commands from `src/vs/editor/browser/coreCommands.ts`, the model's tokenization (`src/vs/editor/common/tokens`,
`model/textModelTokens.ts`, `model/tokens`), the Monarch tokenizer, the token themes and the built-in themes
(`src/vs/editor/standalone/common`, `languages/supports/tokenization.ts`), the language registry, the bracket pairs
and their colorization (`model/bracketPairsTextModelPart`), the indent and bracket pair guides
(`model/guidesTextModelPart.ts`), with the bracket highlight colors of
`core/editorColorRegistry.ts`, the language feature provider types and registries
(`languages.ts`, `languageFeatureRegistry.ts`, `languageSelector.ts`), the marker service from `src/vs/platform/markers`,
the editor features of `src/vs/editor/contrib` (find, folding, lines operations, comment, bracket matching, indentation,
multicursor, word highlighter, clipboard, drag and drop, links, unicode highlighter, snippet, suggest, hover, gotoError,
parameter hints, code action, rename, goto symbol, peek view, code lens, inlay hints, inline completions, semantic
tokens, sticky scroll, document symbols, color picker, format, linked editing and others), the minimap character
renderer and overview zones from `src/vs/editor/browser/viewParts`, the diff algorithms from `src/vs/editor/common/diff`
and `src/vs/base/common/diff`, the fuzzy filters from `src/vs/base/common/filters.ts`, and the unit tests
that go with them. Each ported file names the upstream file and commit it came from. Visual Studio
Code is under the MIT license:

```
MIT License

Copyright (c) 2015 - present Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

"Monaco" and "Visual Studio Code" are names of Microsoft's products; TKCode.Arc is not affiliated with or endorsed by
Microsoft.

The built-in languages of `TKCode.Arc.Editor` (`src/TKCode.Arc.Editor/Languages/BuiltIn`) are generated from the
language definitions of monaco-editor (https://github.com/microsoft/monaco-editor, `src/languages/definitions`: their
Monarch grammars, language configurations and registrations), and the editor's tests replay those definitions' own
tokenization tests (`tests/TKCode.Arc.Editor.Tests/Languages/BuiltIn/TestData`). monaco-editor is under the MIT license,
with the same text and copyright (Copyright (c) 2016 - present Microsoft Corporation) as above.

## three.js — `TKCode.Arc.Graphics3D`

`TKCode.Arc.Graphics3D` is an independent implementation for .NET whose module layout, object model, lighting and
material formulas, loader behavior and camera-control behavior follow three.js (https://threejs.org), and parts of its
shader code and geometry generators are adapted from it
([ADR 0051](design/decisions/0051-graphics3d-package.md)). three.js is under the MIT license:

```
The MIT License

Copyright © 2010-2026 three.js authors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

The tone mapping curves in its shader library are the published ones: ACES filmic (fit by Stephen Hill), AgX (Troy
Sobotka; fit by Benjamin Wrensch) and Khronos PBR Neutral.

## wgpu-native — `TKCode.Arc.Graphics3D.WebGpu`

`TKCode.Arc.Graphics3D.WebGpu` calls wgpu-native (https://github.com/gfx-rs/wgpu-native, © the gfx-rs developers, under
the MIT license or the Apache License 2.0 at your option) through its C API. The package contains no part of
wgpu-native: the application supplies the `libwgpu_native` library (the bindings were written against release
v29.0.1.1) and distributes it under those terms. The structure layouts and constant values in the package's bindings
follow wgpu-native's `wgpu.h` and the `webgpu.h` header of the webgpu-headers project
(https://github.com/webgpu-native/webgpu-headers, © 2019-2023 WebGPU-Native developers, BSD 3-Clause license).

## earcut — `TKCode.Arc.Graphics3D`

The polygon triangulator of `ShapeGeometry` and `ExtrudeGeometry` (`ShapeTriangulator`) implements the algorithm of
Mapbox earcut (https://github.com/mapbox/earcut), which is under the ISC license:

```
ISC License

Copyright (c) 2016-2024, Mapbox

Permission to use, copy, modify, and/or distribute this software for any purpose
with or without fee is hereby granted, provided that the above copyright notice
and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS
OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF
THIS SOFTWARE.
```

## meshoptimizer — `TKCode.Arc.Graphics3D`

`MeshoptDecoder` reads the data formats of the meshoptimizer library (https://github.com/zeux/meshoptimizer), used by the
glTF extensions `EXT_meshopt_compression` and `KHR_meshopt_compression`. It is an independent implementation that follows
the library's decoders. meshoptimizer is under the MIT license:

```
MIT License

Copyright (c) 2016-2026 Arseny Kapoulkine

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Draco — `TKCode.Arc.Graphics3D`

`DracoDecoder` (`Loaders/Draco/`) reads the Draco mesh compression format used by the glTF extension
`KHR_draco_mesh_compression`. It is a C# implementation written for this library that follows the bitstream
specification and the algorithms of the reference decoder (https://github.com/google/draco). Draco is Copyright
The Draco Authors and licensed under the Apache License, Version 2.0 (https://www.apache.org/licenses/LICENSE-2.0):

```
Licensed under the Apache License, Version 2.0 (the "License"); you may not
use this file except in compliance with the License. You may obtain a copy of
the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
License for the specific language governing permissions and limitations under
the License.
```

## Basis Universal and Zstandard — `TKCode.Arc.Graphics3D`

`Ktx2Decoder` (`Imaging/Ktx2/`) reads KTX 2.0 textures with Basis Universal data (ETC1S / BasisLZ and UASTC), as the
glTF extension `KHR_texture_basisu` uses them, and turns them into RGBA pixels. It is a C# implementation written for
this library that follows the KTX 2.0 specification and the reference transcoder
(https://github.com/BinomialLLC/basis_universal); the UASTC tables are taken from that source. Basis Universal is
Copyright Binomial LLC and licensed under the Apache License, Version 2.0 (the notice under Draco above applies).

The Zstandard decompressor in the same folder follows RFC 8878 and the educational decoder of the Zstandard project
(https://github.com/facebook/zstd, `doc/educational_decoder`), which is under the BSD license:

```
BSD License

For Zstandard software

Copyright (c) Meta Platforms, Inc. and affiliates. All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

 * Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

 * Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

 * Neither the name Facebook, nor Meta, nor the names of its contributors may
   be used to endorse or promote products derived from this software without
   specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## Dependencies

- Avalonia (MIT).
