# Vendored Plotly.js

- Version: **4.1.2**, upstream `gl3d` minified distribution; no local bundle build.
- Exact asset URL: https://cdn.plot.ly/plotly-gl3d-4.1.2.min.js
- Asset: `plotly-gl3d-4.1.2.min.js`
- SHA-256: `6e6a2839437ffefa07b1ed727acd89b79019471588af1f558fc5d82c252e4795`
- License: MIT; accompanying unmodified `LICENSE.plotly` from https://raw.githubusercontent.com/plotly/plotly.js/v4.1.2/LICENSE.
- Attribution: Plotly Technologies Inc.; preserve the distribution's embedded copyright and third-party license notices.
- Upstream bundle documentation: https://github.com/plotly/plotly.js/blob/v4.1.2/dist/README.md.

The official gl3d distribution contains both `scatter` and `scatter3d`. Browser validation also checks these actual registered modules. The application loads only this local copy and uses no CDN, Chart Studio, export, or online service at runtime.

The upstream bundle includes embedded shader strings with trailing spaces. Its scoped `.gitattributes` entry disables text conversion and textual diffing so the exact upstream bytes and checksum survive cross-platform checkouts. Do not reformat this asset.

Verify with `shasum -a 256 src/Lpb.Visualizer/wwwroot/vendor/plotly-gl3d-4.1.2.min.js` on macOS or `Get-FileHash src/Lpb.Visualizer/wwwroot/vendor/plotly-gl3d-4.1.2.min.js -Algorithm SHA256` in PowerShell. Dependency updates require deliberate version/hash/license review.
