using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DogSimulator.Networking
{
    internal sealed class BrushColorPicker : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float InnerRadius = .385f;
        private const float MarkerRadius = .443f;
        private const float SquareSize = .49f;
        private const int SquareResolution = 64;
        private static readonly Vector2 Center = Vector2.one * .5f;

        private enum DragRegion { None, Hue, SaturationValue }

        private readonly Color32[] squarePixels = new Color32[SquareResolution * SquareResolution];
        private RectTransform picker;
        [SerializeField] private RectTransform hueMarker;
        [SerializeField] private RectTransform squareMarker;
        [SerializeField] private UnityEngine.UI.RawImage wheelImage;
        [SerializeField] private UnityEngine.UI.RawImage squareImage;
        [SerializeField] private UnityEngine.UI.RawImage hueMarkerImage;
        [SerializeField] private UnityEngine.UI.RawImage squareMarkerImage;
        private Texture2D wheelTexture;
        private Texture2D squareTexture;
        private Texture2D markerTexture;
        private Action<Color> changed;
        private float hue;
        private float saturation;
        private float value;
        private DragRegion dragRegion;
        private int pointerId;

        public void Initialize(Color initialColor, Action<Color> onChanged)
        {
            picker = (RectTransform)transform;
            changed = onChanged;
            wheelTexture = MakeTexture("Brush hue ring", 256, (point, pixel) =>
            {
                var offset = point - Center;
                var radius = offset.magnitude;
                var color = Color.HSVToRGB(HueAt(offset), 1, 1);
                color.a = Mathf.Clamp01(.5f + Mathf.Min(radius - InnerRadius, .5f - radius) / pixel);
                return color;
            });
            wheelImage.texture = wheelTexture;
            squareTexture = NewTexture("Brush saturation and value", SquareResolution);
            squareImage.texture = squareTexture;

            markerTexture = MakeTexture("Brush color cursor", 32, (point, pixel) =>
            {
                var radius = (point - Center).magnitude;
                var color = radius > .37f ? Color.black : Color.white;
                color.a = Mathf.Clamp01(.5f + Mathf.Min(radius - .25f, .48f - radius) / pixel);
                return color;
            });
            hueMarkerImage.texture = squareMarkerImage.texture = markerTexture;

            Color.RGBToHSV(initialColor, out hue, out saturation, out value);
            UpdateSquare();
            PresentColor();
        }

        public void SetColor(Color color)
        {
            Color.RGBToHSV(color, out var nextHue, out saturation, out value);
            if (saturation > 0 && !Mathf.Approximately(hue, nextHue))
            {
                hue = nextHue;
                UpdateSquare();
            }
            PresentColor();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || dragRegion != DragRegion.None
                || !TryGetPoint(eventData, out var point)) return;

            var radius = point.magnitude;
            if (radius >= InnerRadius && radius <= .5f)
                dragRegion = DragRegion.Hue;
            else if (Mathf.Abs(point.x) <= SquareSize * .5f && Mathf.Abs(point.y) <= SquareSize * .5f)
                dragRegion = DragRegion.SaturationValue;
            else return;

            pointerId = eventData.pointerId;
            UpdateSelection(point);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragRegion != DragRegion.None && pointerId == eventData.pointerId && TryGetPoint(eventData, out var point))
                UpdateSelection(point);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointerId == eventData.pointerId) dragRegion = DragRegion.None;
        }

        private void OnDisable() => dragRegion = DragRegion.None;

        private bool TryGetPoint(PointerEventData eventData, out Vector2 point)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(picker, eventData.position,
                eventData.pressEventCamera, out var local))
            {
                var rect = picker.rect;
                point = (local - rect.center) / rect.size;
                return true;
            }
            point = default;
            return false;
        }

        private void UpdateSelection(Vector2 point)
        {
            if (dragRegion == DragRegion.Hue)
            {
                if (point.sqrMagnitude < .0001f) return;
                hue = HueAt(point);
                UpdateSquare();
            }
            else
            {
                var squarePoint = point / SquareSize + Center;
                saturation = Mathf.Clamp01(squarePoint.x);
                value = Mathf.Clamp01(squarePoint.y);
            }
            PresentColor();
        }

        private void PresentColor()
        {
            var angle = hue * Mathf.PI * 2;
            PlaceMarker(hueMarker, Center + MarkerRadius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            PlaceMarker(squareMarker, Center + (new Vector2(saturation, value) - Center) * SquareSize);
            changed(Color.HSVToRGB(hue, saturation, value));
        }

        private void UpdateSquare()
        {
            var pureHue = Color.HSVToRGB(hue, 1, 1);
            for (var y = 0; y < SquareResolution; y++)
            for (var x = 0; x < SquareResolution; x++)
            {
                var s = (float)x / (SquareResolution - 1);
                var v = (float)y / (SquareResolution - 1);
                var color = Color.Lerp(Color.white, pureHue, s) * v;
                color.a = 1;
                squarePixels[y * SquareResolution + x] = color;
            }
            squareTexture.SetPixels32(squarePixels);
            squareTexture.Apply(false);
        }

#if UNITY_EDITOR
        internal void BakeImages()
        {
            wheelImage = gameObject.AddComponent<UnityEngine.UI.RawImage>();
            squareImage = AddImage("Saturation and Value");
            var square = squareImage.rectTransform;
            square.anchorMin = Vector2.one * ((1 - SquareSize) * .5f);
            square.anchorMax = Vector2.one - square.anchorMin;
            square.sizeDelta = Vector2.zero;
            hueMarkerImage = AddImage("Hue Cursor");
            squareMarkerImage = AddImage("Saturation and Value Cursor");
            hueMarker = hueMarkerImage.rectTransform;
            squareMarker = squareMarkerImage.rectTransform;
            hueMarker.sizeDelta = squareMarker.sizeDelta = Vector2.one * 18;
        }

        private UnityEngine.UI.RawImage AddImage(string name)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<UnityEngine.UI.RawImage>();
            image.rectTransform.SetParent(transform, false);
            image.raycastTarget = false;
            return image;
        }
#endif

        private static void PlaceMarker(RectTransform marker, Vector2 anchor)
        {
            marker.anchorMin = marker.anchorMax = anchor;
            marker.anchoredPosition = Vector2.zero;
        }

        private static float HueAt(Vector2 point) => Mathf.Repeat(Mathf.Atan2(point.y, point.x) / (Mathf.PI * 2), 1);

        private static Texture2D NewTexture(string name, int size)
            => new(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };

        private static Texture2D MakeTexture(string name, int size, Func<Vector2, float, Color> sample)
        {
            var texture = NewTexture(name, size);
            var pixels = new Color32[size * size];
            var pixel = 1f / size;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = sample(new Vector2(x + .5f, y + .5f) * pixel, pixel);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void OnDestroy()
        {
            Destroy(wheelTexture);
            Destroy(squareTexture);
            Destroy(markerTexture);
        }
    }
}
