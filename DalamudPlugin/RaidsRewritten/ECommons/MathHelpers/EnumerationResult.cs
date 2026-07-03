// https://github.com/NightmareXIV/ECommons/blob/master/ECommons/MathHelpers/EnumerationResult.cs
// 2361541
namespace ECommons.MathHelpers;

public readonly record struct EnumerationResult<T>(T Object, float AngleDegrees);