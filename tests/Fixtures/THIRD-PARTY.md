# Third-party files in tests/Fixtures

Binaries from other projects, kept here so the tests and the LMP client patches build without downloading anything.
Each one is unchanged from the official release named below; check with `Get-FileHash <file>` (SHA-256).

| File | From | SHA-256 |
|---|---|---|
| `LmpClient/Lidgren.Network.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) `LunaMultiplayer-Client-Release.zip` (zip SHA-256 `d944ec93df1891dbc136d0d59b69ba9adc8c215d1c0cbaa15b7742ad0babb9c4`), MIT | `e757c62f9b3251ce2d4d68464ac3ba687e6df37c35a69ab6b26daa252371e227` |
| `LmpClient/LmpClient.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) `LunaMultiplayer-Client-Release.zip` (zip SHA-256 `d944ec93df1891dbc136d0d59b69ba9adc8c215d1c0cbaa15b7742ad0babb9c4`), MIT | `d305756361326f26b42007e09c356839f2eb2910420ae911c8deb2683c668116` |
| `LMPServer/ByteSize.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `fd403b550bf182f3dc8ad527beebcf53eccf72994e1815619ba56eaf30120f7b` |
| `LMPServer/CachedQuickLz.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `6760b027f7bdb392a414ce665e27973b55734e6aff85daf750676b64650c0c84` |
| `LMPServer/DotNetZip.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `4e758794ad61c3cdd96bfcecdc64a5970ceec5685f9b47bbc8285547365aabfd` |
| `LMPServer/Humanizer.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `15e988ab3e8d84900ae90549eb399aac452d55edf0109e06fa1a9b227ddfd4c4` |
| `LMPServer/Lidgren.Core.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `9aaacfc1b9fb5733c2651cc8ddda4c78f12fae880a55733200945638bbb5514a` |
| `LMPServer/LmpUpdater.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `a29b1704af2d586284f4a9305837fa7c56f7790e410446ec6e352939b01b0b8e` |
| `LMPServer/LunaConfigNode.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `56eb5f5ff6e43d4a979c11d25e6b0bf4c37df6844751ba3356f4a878393fea2a` |
| `LMPServer/Microsoft.Bcl.AsyncInterfaces.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `00f26117b0a77b562d5c01038d8b52f8d0b9e18b7c532e16dfae1857a19f2271` |
| `LMPServer/Microsoft.VisualStudio.Threading.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `e483a502f71186e576a3a40e7afbab318fbd9ac1d33278d472c06449aa99099a` |
| `LMPServer/Microsoft.VisualStudio.Validation.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `7efa4f40cd3864a9c4cec616b0cfb8a07bdde614055f46be41fe4119cf6f249d` |
| `LMPServer/Microsoft.Win32.SystemEvents.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `ca495bf6c86b7f092d624928101985a941532d149dcd8acfea170f8a53be8850` |
| `LMPServer/Newtonsoft.Json.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `99177a4cbe03625768d64a3d73392310372888f74c3eb271cf775e93057a38e6` |
| `LMPServer/Open.Nat.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `45e0d343e6857e879022aac6c451c194063342bea7484fc45f93adf964392ae7` |
| `LMPServer/Server.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `5c350a6228e9f6c4a795a575e58a13542f4ca2961452c50084a68661ea17b514` |
| `LMPServer/System.Configuration.ConfigurationManager.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `e814b79f175ecde855c7ae003cd8bc5ed88edea4ab4089d055ed7b63da7bbec9` |
| `LMPServer/System.Drawing.Common.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `98d07e6b1e7d3641a8608a360b3dc273fe8a0f78926b6e1e5f679a7a074d491f` |
| `LMPServer/System.Security.Cryptography.ProtectedData.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `80566f0839ece5946e66bf9f00d723e59e371ba1341a18e00c7c7a7c49298e1d` |
| `LMPServer/System.Security.Permissions.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `016fec1bc29ce2e3420dcbc95b70b892761fe6404f8dd451f1b57999bf00e298` |
| `LMPServer/System.Windows.Extensions.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `05b1b701c97c89799f1372ee36f6ca612e97da875404aabfac47678fe746128b` |
| `LMPServer/log4net.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `33909e473b3225081106cfaf03f2fbb0a564d5a61a41ac5c35603f165e44e638` |
| `LMPServer/uhttpsharp.dll` | [LMP 0.29.2](https://github.com/LunaMultiplayer/LunaMultiplayer/releases/tag/0.29.2) server release, MIT and its dependencies' licenses | `09e247077535bf4bb42e06b3639b4c3af2e45f9afaca42729ca50bf3bb1f3a76` |

Licenses: `LmpClient/LMP-LICENSE.txt`, `LMPServer/LMP-LICENSE.txt`. No KSP or Unity files are here: the client patches build against
LUNA EVO's own stand-ins (`src/LunaEvo.ClientPatches/Stubs`).
