using UnityEngine;

namespace Game.Client
{
    public static class ProceduralSprites
    {
        const int Size = 128;

        static Sprite disc;
        static Sprite ring;

        public static Sprite Disc => disc ??= Build(false);

        public static Sprite Ring => ring ??= Build(true);

        static Sprite Build(bool hollow)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[Size * Size];
            var center = (Size - 1) * 0.5f;
            var radius = Size * 0.5f - 1f;
            var inner = radius - 7f;

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    var alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    if (hollow)
                    {
                        alpha *= Mathf.Clamp01(distance - inner + 0.5f);
                    }

                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
