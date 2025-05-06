// SyncShared, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// Noise.PatternModifiers

namespace Noise;

[Flags]
public enum PatternModifiers
{
    None = 0,
    Fallback = 1,
    Psk0 = 2,
    Psk1 = 4,
    Psk2 = 8,
    Psk3 = 0x10
}