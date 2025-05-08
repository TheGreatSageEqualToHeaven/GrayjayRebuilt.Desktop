# GrayjayRebuilt.Desktop

A decompiled version of Grayjay.Desktop that can be compiled with your own changes. Decompiled using ilspy.

### Build instructions

- Build `Grayjay.Desktop.CEF` with dotnet on `release` configuration.
- Copy `cef` and `wwwroot` from the futo release of `Grayjay.Desktop` to the build directory.
- You have rebuilt Grayjay.

  **additional steps for linux and mac**: libsodium.dll is the only shared library provided for `SyncShared`, empty templates for `libsodium.dylib` and `libsodium.so` are in the project that you will need to replace.  

  **note**: All compiled executables in this project are from `futo-org`'s github, gitlab or existing releases.  
  
### Why?

I wanted to have Grayjay always on top.

![image](https://github.com/user-attachments/assets/b5821ce0-4d4c-41c5-8eba-3927495773ca)
