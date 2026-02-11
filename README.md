# ResoniteSpout
[![Thunderstore Badge](https://modding.resonite.net/assets/available-on-thunderstore.svg)](https://thunderstore.io/c/resonite/)

A [Resonite](https://resonite.com/) mod that enables real-time camera output streaming via [Spout](https://spout.zeal.co/) for seamless integration with OBS, vMix, and other video production software.

## Features

- 🎥 **Multiple Camera Support** - Stream multiple Resonite cameras simultaneously
- 🏷️ **Dynamic Camera Naming** - Customize Spout sender names for easy identification
- ⚙️ **Configurable Prefixes** - Filter cameras by user-specific prefixes
- 🔄 **Automatic Management** - Cameras are automatically created and cleaned up
- 🚀 **Zero Latency** - Direct GPU texture sharing for minimal performance impact

## Installation

### Automatic (Recommended)
1. Install [BepisLoader](https://github.com/ResoniteModding/BepisLoader) for Resonite
2. Install via Thunderstore mod manager or download from [Releases](https://github.com/Zozokasu/ResoniteSpout/releases)

## Usage

### Basic Setup

1. Open an inspector on the same slot as the `RenderTextureProvider` of your camera
1. Create a `DynamicVariableSpace` component and name it `ResoniteSpout.YourName`
1. Add a `DynamicReferenceVariable<RenderTextureProvider>` named `TargetRTP`
1. Connect your camera's render texture to the variable
1. The Spout sender will appear in OBS/vMix as `[ResoSpout] YourName`

### Advanced: Custom Camera Names

Optionally add a `DynamicValueVariable<string>` named `CameraName`:
```
ResoniteSpout.YourName
├── TargetRTP (RenderTextureProvider) → Your camera's render texture
└── CameraName (string) → "MainCamera"
```

The Spout sender will appear as `[ResoSpout] YourName - MainCamera`

### Multiple Cameras

You can create multiple camera setups with different names:

- `ResoniteSpout.Studio1` → Appears as `[ResoSpout] Studio1`
- `ResoniteSpout.Studio2` → Appears as `[ResoSpout] Studio2`

### Example Camera

Try example camera resonite package! [here](https://github.com/Zozokasu/ResoniteSpout/raw/refs/heads/master/CameraExample.resonitepackage)

![OBS screen after spawning the sample camera](https://github.com/user-attachments/assets/3b8550b1-680a-433f-b380-cd1aba17de92)
## Configuration

Edit `BepInEx/config/zozokasu.ResoniteSpout.Engine.cfg`:
```ini
[General]
# Filter to only monitor specific cameras
# Empty = Monitor all ResoniteSpout.* spaces
# Example: "MyName" = Only monitor "ResoniteSpout.MyName"
SpacePrefix = 
```

## OBS Setup

1. Add a **Spout2 Capture** source to your scene
2. Select your Resonite camera from the dropdown (e.g., `[ResoSpout] YourName`)
3. Done! The feed updates in real-time

## Requirements

- Resonite with BepInEx
- [BepisResoniteWrapper](https://thunderstore.io/c/resonite/p/ResoniteModding/BepisResoniteWrapper/)
- [RenderiteHook](https://thunderstore.io/c/resonite/p/ResoniteModding/RenderiteHook/)
- [InterprocessLib](https://thunderstore.io/c/resonite/p/ResoniteModding/InterprocessLib/)
- Windows with Spout-compatible graphics card

## Use Cases

- 🎬 **Live Streaming** - Capture Resonite worlds directly in OBS
- 🎮 **Game Development** - Preview in-game cameras in real-time
- 📺 **Virtual Production** - Integrate Resonite into broadcast workflows
- 🎨 **Content Creation** - Record high-quality footage without screen capture

## Troubleshooting

**Camera not appearing in OBS:**
- Verify the `DynamicVariableSpace` name starts with `ResoniteSpout.`
- Ensure the `DynamicReferenceVariable<RenderTextureProvider>` is on the same slot as the `RenderTextureProvider` itself
- Move the `DynamicVariableSpace` to another slot and back again
- Check BepInEx logs for errors: `BepInEx/LogOutput.log`
- Ensure both Engine and Renderer plugins are installed

**Multiple cameras conflict:**
- Each `DynamicVariableSpace` must have a unique name
- Use different suffixes (e.g., `ResoniteSpout.Camera1`, `ResoniteSpout.Camera2`)

## Building from Source
```bash
git clone https://github.com/Zozokasu/ResoniteSpout.git
cd ResoniteSpout
dotnet build -c Release
```

Output files will be in `out/`

## Credits

Built with:
- [Spout](https://spout.zeal.co/) - GPU texture sharing
- [InterprocessLib](https://github.com/Nytra/ResoniteInterprocessLib) by Nytra
- [RenderiteHook](https://github.com/ResoniteModding/RenderiteHook) by ResoniteModding

## License

MIT License - See [LICENSE](LICENSE) for details

## Support

- 🐛 **Issues:** [GitHub Issues](https://github.com/Zozokasu/ResoniteSpout/issues)
- 💬 **Discord:** Resonite Modding Community
- 📖 **Docs:** [Resonite Modding Wiki](https://modding.resonite.net/)
