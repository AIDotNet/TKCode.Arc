# Third-party notices: pdf.js in TKCode.Arc.Pdf.Browser

`TKCode.Arc.Pdf.Browser` ships files from the npm package [`pdfjs-dist`](https://www.npmjs.com/package/pdfjs-dist) version 6.4.299 (`tools/pdf/sync-pdfjs.sh` downloads it and verifies its SHA-256). The package carries the complete license text of each component, unabridged, next to its files under `staticwebassets/pdfjs/` (the table below names the file, relative to that folder); a host serves them at `_content/TKCode.Arc.Pdf.Browser/pdfjs/`. An application that ships these files must carry these notices.

| Component | Files | License | Copyright, as the license file states it | License file |
|---|---|---|---|---|
| PDF.js | `pdf.min.mjs`, `pdf.worker.min.mjs` | Apache-2.0 | Mozilla Foundation (in the header of both files; `LICENSE` has no holder) | `LICENSE` |
| Adobe CMap resources | `cmaps/*.bcmap` | BSD-3-Clause | Adobe Systems Incorporated | `cmaps/LICENSE` |
| Foxit fonts of PDFium | `standard_fonts/Foxit*.pfb` | BSD-3-Clause | The PDFium Authors | `standard_fonts/LICENSE_FOXIT` |
| CMYK profile | `iccs/CGATS001Compat-v2-micro.icc` | CC0-1.0 | Not stated: the file is the CC0 deed | `iccs/LICENSE` |
| OpenJPEG, for JPEG 2000 images | `wasm/openjpeg.wasm` | BSD-2-Clause | Universite catholique de Louvain, Benoit Macq, Antonin Descampe, Francois-Olivier Devaux, Herve Drolon, Yannick Verschueren, David Janssens, CNES, CS Systemes d'Information | `wasm/LICENSE_OPENJPEG` |
| PDF.js wrapper of OpenJPEG | `wasm/openjpeg.wasm` | BSD-2-Clause | Mozilla Foundation | `wasm/LICENSE_PDFJS_OPENJPEG` |
| PDFium's JBIG2 decoder | `wasm/jbig2.wasm` | BSD-3-Clause and Apache-2.0 (the file holds both texts and does not say which part is under which) | The PDFium Authors | `wasm/LICENSE_JBIG2` |
| PDF.js wrapper of the JBIG2 decoder | `wasm/jbig2.wasm` | Apache-2.0 | Mozilla Foundation | `wasm/LICENSE_PDFJS_JBIG2` |
| qcms, for color management | `wasm/qcms_bg.wasm` | MIT | Mozilla Corporation, Marti Maria | `wasm/LICENSE_QCMS` |
| PDF.js wrapper of qcms | `wasm/qcms_bg.wasm` | MIT | Not stated in the file | `wasm/LICENSE_PDFJS_QCMS` |

## Left out of this package

The tarball has more than this package ships. Nothing below is in the package, and no license of it applies here.

- **`standard_fonts/LiberationSans-*.ttf`** are under the GNU General Public License version 2 with font exceptions (`LICENSE_LIBERATION` in the tarball), unlike everything above. Without them, pdf.js draws a document's non-embedded Helvetica or Arial in the local Helvetica or Arial when the device has one and otherwise in the browser's sans-serif, so text stays readable but may look different and sit less exactly.
- `wasm/quickjs-eval.*` and `build/pdf.sandbox.*`: the sandbox for JavaScript in documents, which this viewer never runs.
- `wasm/*_nowasm_fallback.js`: decoders for browsers without WebAssembly, where a .NET WebAssembly app does not start.

## The texts that require reproduction

### Adobe CMap resources (BSD-3-Clause)

```text
Copyright 1990-2009 Adobe Systems Incorporated.
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

Redistributions in binary form must reproduce the above copyright notice, this
list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

Neither the name of Adobe Systems Incorporated nor the names of its
contributors may be used to endorse or promote products derived from this
software without specific prior written permission.

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

### Foxit fonts and the JBIG2 decoder of PDFium (BSD-3-Clause)

`standard_fonts/LICENSE_FOXIT` says "Copyright 2014 PDFium Authors. All rights reserved." and `wasm/LICENSE_JBIG2` says "Copyright 2014 The PDFium Authors":

```text
Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

   * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
   * Neither the name of Google Inc. nor the names of its
contributors may be used to endorse or promote products derived from
this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

### OpenJPEG (BSD-2-Clause)

```text
Copyright (c) 2002-2014, Universite catholique de Louvain (UCL), Belgium
Copyright (c) 2002-2014, Professor Benoit Macq
Copyright (c) 2003-2014, Antonin Descampe
Copyright (c) 2003-2009, Francois-Olivier Devaux
Copyright (c) 2005, Herve Drolon, FreeImage Team
Copyright (c) 2002-2003, Yannick Verschueren
Copyright (c) 2001-2003, David Janssens
Copyright (c) 2011-2012, Centre National d'Etudes Spatiales (CNES), France
Copyright (c) 2012, CS Systemes d'Information, France
All rights reserved.
```

The conditions and the disclaimer are those of the `wasm/LICENSE_OPENJPEG` file, reproduced in full there.

### PDF.js wrapper of OpenJPEG (BSD-2-Clause)

```text
Copyright (c) 2024, Mozilla Foundation
```

The conditions and the disclaimer are those of the `wasm/LICENSE_PDFJS_OPENJPEG` file, reproduced in full there.

### qcms (MIT)

```text
qcms
Copyright (C) 2009-2024 Mozilla Corporation
Copyright (C) 1998-2007 Marti Maria

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software
is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```
