﻿namespace MyGame.Utils;

public static class NumericsExt
{
    public static Num.Vector4 ToNumerics(this Color self) => new(self.R / 255.0f, self.G / 255.0f, self.B / 255.0f, self.A / 255.0f);
    public static Num.Vector4 ToNumerics(this Vector4 self) => new(self.X, self.Y, self.Z, self.W);
    public static Num.Vector3 ToNumerics(this Vector3 self) => new(self.X, self.Y, self.Z);
    public static Num.Vector2 ToNumerics(this Vector2 self) => new(self.X, self.Y);
    public static Num.Vector2 ToNumerics(this Point self) => new(self.X, self.Y);
    public static Color ToColor(this Num.Vector4 self) => new(self.X, self.Y, self.Z, self.W);
    public static Vector4 ToXNA(this Num.Vector4 self) => new(self.X, self.Y, self.Z, self.W);
    public static Vector3 ToXNA(this Num.Vector3 self) => new(self.X, self.Y, self.Z);
    public static Vector2 ToXNA(this Num.Vector2 self) => new(self.X, self.Y);
}