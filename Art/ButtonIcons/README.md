# Astra Express generated button art

Generated on 2026-09-13 with the built-in ChatGPT image generation tool. The available tool does not expose a model selector, so this delivery does not claim that the backend was Image 2.5. Each distinct concept was generated in its own call. The exact prompts are preserved in `./prompts.json`, and original tool output paths are preserved in `./manifest.json`.

## Integration assets

- `./`: 15 original generated RGBA PNGs, 1254 × 1254 pixels each.
- `../../Assets/Resources/UI/Icons/`: 256 × 256 RGBA PNG derivatives, 787,150 bytes total. Recommended Unity resource source.
- `../../Assets/WebGLTemplates/Astra/icons/`: 128 × 128 RGBA PNG derivatives, 262,586 bytes total. Recommended browser coach source.
- `./contact-sheet.png`: review sheet with each icon shown at 128, 32, and 20 pixels against a dark HUD color.
- `./validation.json`: alpha and dimension checks for every original.

Derivatives use ordinary Lanczos downsampling and PNG compression; artwork and generated transparency are preserved. No recoloring, redrawing, or synthetic background removal was performed. Every original has real alpha with fully transparent and fully opaque pixels.

## Button mapping

| PNG basename | Button actions | Suggested size |
| --- | --- | --- |
| `rover` | Explore, focus Rover | 32px toolbar; 20px focus button |
| `extractor` | Build Extractor | 32px |
| `solar` | Build Solar | 32px |
| `conduit` | Lay Conduit, show a power connection | 32px toolbar; 20px coach |
| `rail` | Lay Rail, show a rail connection | 32px toolbar; 20px coach |
| `train` | Train / Upgrades, Train Running status | 32px toolbar; 20px status |
| `colony` | Focus Colony | 20px |
| `pause` | Pause game, Pause Mine | 20px |
| `play` | Resume game, Resume Mine, Resume Exploration, Dispatch Train | 20px |
| `restart` | Restart | 20px |
| `upgrade` | Upgrade Extractor, increase train capacity | 20px |
| `park` | Park at Colony, park current service first | 20px |
| `focus` | Pip Show me | 20px |
| `ask` | Ask Pip, What next? | 20px |
| `close` | Dismiss Pip, Hide link guide | 20px |

Keep the existing Pip avatar for its launcher. Numbered world markers should retain their large numbers because their order is their meaning; these icons are for action buttons rather than replacing step numbers.

## Visual review

All 15 originals and the final contact sheet were visually inspected. The ivory/mint palette and dark inset materials are consistent. The main construction shapes remain recognizable at 32px; pause, play, restart, focus, ask, close, and upgrade remain readable at 20px. Rover and train share a vehicle silhouette, so retain their labels; the rover antenna and train headlamp are secondary distinguishing details. Park is a stop/docking symbol and also needs its label. Do not rely on the mint color or icon alone to explain an action.

UI integration and actual game testing are performed by the parent task; this art subtask did not modify the live game, run a Unity build, or interact with the player's session.
