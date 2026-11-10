using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DogSimulator.Networking
{
    [Serializable]
    internal sealed class DogMenuSkin
    {
        public static readonly Color Brown = new(.27f, .15f, .09f);
        public static readonly Color Cream = new(1f, .94f, .83f);
        public static readonly Color Cocoa = new(.51f, .34f, .23f);
        public static readonly Color Muted = new(.53f, .39f, .30f);
        public static readonly Color HudText = new Color32(48, 58, 54, 255);
        public static readonly Color HudAlertText = new Color32(128, 61, 32, 255);
        public const float HudMargin = 24;
        public const float HudGap = 8;
        public const float HudRow = 36;
        public const float HudType = 18;

        [field: SerializeField] public Sprite Paper { get; private set; }
        [field: SerializeField] public Sprite Primary { get; private set; }
        [field: SerializeField] public Sprite HudPill { get; private set; }
        [field: SerializeField] public Sprite Paw { get; private set; }
        [field: SerializeField] public Sprite Bone { get; private set; }
#if UNITY_EDITOR
        private TMP_FontAsset font;

        public DogMenuSkin(Func<Sprite, Sprite> saveSprite, Func<TMP_FontAsset> loadFont)
        {
            Paper = RoundSprite("Biscuit", new Color(1f, .98f, .92f), new Color(.94f, .86f, .74f));
            Primary = RoundSprite("Honey", new Color(1f, .90f, .47f), new Color(1f, .68f, .15f));
            var hudSurface = new Color32(248, 242, 230, 255);
            HudPill = RoundSprite("HUD Hint", hudSurface, hudSurface, new Color32(200, 189, 169, 255));
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
            Paper = saveSprite(Paper);
            Primary = saveSprite(Primary);
            HudPill = saveSprite(HudPill);
            Paw = saveSprite(Paw);
            Bone = saveSprite(Bone);
            font = loadFont();
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

        public UnityEngine.UI.Button Button(RectTransform rect, string caption, UnityEngine.Events.UnityAction action, bool primary = false,
            float fontSize = 30, Sprite icon = null)
        {
            var button = MakeButton(rect, caption, primary, fontSize, icon);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        public UnityEngine.UI.Button Button(RectTransform rect, string caption, UnityEngine.Events.UnityAction<int> action,
            int argument, bool primary, float fontSize, Sprite icon = null)
        {
            var button = MakeButton(rect, caption, primary, fontSize, icon);
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(button.onClick, action, argument);
            return button;
        }

        private UnityEngine.UI.Button MakeButton(RectTransform rect, string caption, bool primary, float fontSize, Sprite icon)
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
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f,
                100, 0, SpriteMeshType.FullRect, Vector4.one * border);
            sprite.name = name;
            return sprite;
        }

#endif
    }
}
