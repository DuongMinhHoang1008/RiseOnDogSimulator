using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DogSimulator.Networking
{
    internal sealed class DogMenuSkin : IDisposable
    {
        public static readonly Color Brown = new(.27f, .15f, .09f);
        public static readonly Color Cream = new(1f, .94f, .83f);
        public static readonly Color Cocoa = new(.51f, .34f, .23f);
        public static readonly Color Muted = new(.53f, .39f, .30f);

        public readonly Sprite Paper;
        public readonly Sprite Primary;
        public readonly Sprite HudPill;
        public readonly Sprite Paw;
        public readonly Sprite Bone;
        private readonly TMP_FontAsset font;

        public DogMenuSkin()
        {
            Paper = RoundSprite("Biscuit", new Color(1f, .98f, .92f), new Color(.94f, .86f, .74f));
            Primary = RoundSprite("Honey", new Color(1f, .90f, .47f), new Color(1f, .68f, .15f));
            HudPill = RoundSprite("HUD Hint", new Color(.29f, .23f, .14f, .9f), new Color(.20f, .15f, .09f, .9f), Cream);
            Paw = Icon("Paw", p => Mathf.Min(
                Oval(p, new Vector2(.5f, .35f), new Vector2(.25f, .20f)),
                Mathf.Min(Mathf.Min(Oval(p, new Vector2(.18f, .58f), new Vector2(.085f, .11f)),
                    Oval(p, new Vector2(.38f, .73f), new Vector2(.096f, .13f))),
                    Mathf.Min(Oval(p, new Vector2(.62f, .73f), new Vector2(.096f, .13f)),
                        Oval(p, new Vector2(.82f, .58f), new Vector2(.085f, .11f))))));
            Bone = Icon("Bone", p =>
            {
                var middle = Mathf.Max(Mathf.Abs(p.x - .5f) - .28f, Mathf.Abs(p.y - .5f) - .12f) * 7;
                var left = Mathf.Min(Oval(p, new Vector2(.20f, .64f), new Vector2(.13f, .14f)),
                    Oval(p, new Vector2(.20f, .36f), new Vector2(.13f, .14f)));
                var right = Mathf.Min(Oval(p, new Vector2(.80f, .64f), new Vector2(.13f, .14f)),
                    Oval(p, new Vector2(.80f, .36f), new Vector2(.13f, .14f)));
                return Mathf.Min(middle, Mathf.Min(left, right));
            });
            var source = Resources.Load<Font>("DogMenu/Sniglet");
            if (source != null)
            {
                font = TMP_FontAsset.CreateFontAsset(source);
                if (font != null && TMP_Settings.defaultFontAsset != null)
                    font.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            }
        }

        // All menu pages use the same 1600 × 900 composition area.
        public static RectTransform At(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x / 1600f, 1 - (y + height) / 900f);
            rect.anchorMax = new Vector2((x + width) / 1600f, 1 - y / 900f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public UnityEngine.UI.Image Surface(RectTransform rect, Sprite sprite, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero
                ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public TMP_Text Label(RectTransform rect, string value, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.richText = false;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = new Vector4(5, 0, 5, 0);
            return label;
        }

        public UnityEngine.UI.Button Button(RectTransform rect, string caption, Action action, bool primary = false,
            float fontSize = 30, Sprite icon = null)
        {
            var image = Surface(rect, primary ? Primary : Paper, Color.white, true);
            var shadow = rect.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(.30f, .16f, .07f, .35f);
            shadow.effectDistance = new Vector2(0, -4);
            var button = rect.gameObject.AddComponent<DogMenuButton>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, .97f, .88f);
            colors.pressedColor = new Color(1f, .88f, .67f);
            colors.disabledColor = new Color(.79f, .72f, .64f, .85f);
            button.colors = colors;
            if (icon != null)
                Surface(At(rect, "Icon", 100, 165, 180, 570), icon, Brown).preserveAspect = true;
            Label(At(rect, "Label", icon == null ? 35 : 285, 0, icon == null ? 1530 : 1250, 900),
                caption, fontSize, Brown);
            button.onClick.AddListener(() => action());
            return button;
        }

        public TMP_InputField Input(RectTransform rect, string prompt, string value, int maxLength)
        {
            var background = Surface(rect, Paper, Color.white, true);
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            var viewport = At(rect, "TextArea", 55, 70, 1490, 760);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            input.textViewport = viewport;
            input.textComponent = Label(At(viewport, "Value", 0, 0, 1600, 900), "", 25, Brown, TextAlignmentOptions.Left);
            input.textComponent.overflowMode = TextOverflowModes.Overflow;
            input.placeholder = Label(At(viewport, "Placeholder", 0, 0, 1600, 900), prompt, 25, Muted, TextAlignmentOptions.Left);
            input.richText = false;
            input.characterLimit = maxLength;
            input.text = value;
            return input;
        }

        public UnityEngine.UI.Slider Slider(RectTransform rect, float min, float max, float value)
        {
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            Surface(At(rect, "Track", 0, 330, 1600, 270), Paper, new Color(.78f, .64f, .51f));
            var fillArea = At(rect, "FillArea", 32, 330, 1536, 270);
            var fill = Surface(At(fillArea, "Fill", 0, 0, 1600, 900), Primary, Color.white);
            var handles = At(rect, "HandleArea", 38, 0, 1524, 900);
            var knob = Surface(At(handles, "Handle", 0, 90, 0, 790), Primary, Color.white, true);
            knob.rectTransform.sizeDelta = new Vector2(25, 0);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = knob.rectTransform;
            slider.targetGraphic = knob;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            Surface(rect, null, Color.clear, true);
            return slider;
        }

        private static float Oval(Vector2 p, Vector2 center, Vector2 radii)
            => ((p - center) / radii).magnitude - 1;

        private static Sprite Icon(string name, Func<Vector2, float> distance)
            => MakeSprite(name, 64, 0, (x, y) => new Color(1, 1, 1,
                Mathf.Clamp01(.5f - distance(new Vector2((x + .5f) / 64, (y + .5f) / 64)) * 8f)));

        private static Sprite RoundSprite(string name, Color top, Color bottom, Color? outline = null)
            => MakeSprite(name, 80, 31, (x, y) =>
            {
                var q = new Vector2(Mathf.Abs(x - 39.5f), Mathf.Abs(y - 39.5f)) - Vector2.one * 12;
                var edge = Vector2.Max(q, Vector2.zero).magnitude - 26;
                var fill = Color.Lerp(bottom, top, y / 79f);
                if (outline == null && edge > -5 && edge <= -3 && y > 40) fill = Color.Lerp(fill, Color.white, .78f);
                var pixel = Color.Lerp(outline ?? Cocoa, fill, Mathf.Clamp01(-edge - 1.5f));
                pixel.a *= Mathf.Clamp01(.5f - edge);
                return pixel;
            });

        private static Sprite MakeSprite(string name, int size, float border, Func<int, int, Color> color)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++) pixels[y * size + x] = color(x, y);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f,
                100, 0, SpriteMeshType.FullRect, Vector4.one * border);
        }

        public void Dispose()
        {
            foreach (var sprite in new[] { Paper, Primary, HudPill, Paw, Bone })
            {
                UnityEngine.Object.Destroy(sprite.texture);
                UnityEngine.Object.Destroy(sprite);
            }
            if (font == null) return;
            foreach (var atlas in font.atlasTextures) UnityEngine.Object.Destroy(atlas);
            UnityEngine.Object.Destroy(font.material);
            UnityEngine.Object.Destroy(font);
        }
    }

    internal sealed class DogMenuButton : UnityEngine.UI.Button
    {
        private float targetScale = 1;

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            targetScale = state == SelectionState.Pressed ? .95f : state == SelectionState.Highlighted ? 1.025f : 1;
        }

        private void Update()
        {
            var scale = Mathf.Lerp(transform.localScale.x, targetScale, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 22));
            transform.localScale = new Vector3(scale, scale, 1);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            transform.localScale = Vector3.one;
            targetScale = 1;
        }
    }
}
