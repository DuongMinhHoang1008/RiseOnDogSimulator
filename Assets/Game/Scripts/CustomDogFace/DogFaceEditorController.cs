using System;
using System.Collections.Generic;
using DogSimulator.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DogSimulator.CustomDogFace
{
    public sealed class DogFaceEditorController : MonoBehaviour, IPointerDownHandler,
        IPointerUpHandler, IDragHandler, IScrollHandler
    {
        private enum Mode { Align, Draw, Erase }

        [Serializable]
        private sealed class Stroke
        {
            public readonly List<Vector2> points = new();
            public Color color;
            public float size;
            public bool erase;
        }

        [SerializeField] private GameObject modal;
        [SerializeField] private RawImage canvas;
        [SerializeField] private Slider zoomSlider;
        [SerializeField] private Slider rotationSlider;
        [SerializeField] private Slider brushSizeSlider;
        [SerializeField] private TMP_InputField colorHex;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text applyLabel;
        [SerializeField] private DogSelectionPreview preview;
        private readonly List<Stroke> strokes = new();
        private Texture2D source;
        private Texture2D composite;
        private Color[] basePixels;
        private Color[] pixels;
        private Vector2 offset;
        private float scale = 1;
        private float rotation;
        private Mode mode;
        private Stroke activeStroke;
        private bool dirty;
        private bool alignmentDirty = true;
        private bool connected;
        private Slider positionX;
        private Slider positionY;
        private bool livePreview;

        public bool IsOpen => modal != null && modal.activeSelf;
        public event Action<byte[]> Applied;
        public event Action DefaultRequested;
        public event Action Opened;
        public event Action Closed;

        private void Awake()
        {
            if (modal != null) modal.SetActive(false);
            if (zoomSlider != null) zoomSlider.onValueChanged.AddListener(SetScale);
            if (rotationSlider != null) rotationSlider.onValueChanged.AddListener(SetRotation);
        }

        public void ConfigureMenu(GameObject page, RawImage drawingCanvas, DogSelectionPreview modelPreview,
            TMP_Text feedback, Slider size, Slider x, Slider y, Slider angle, Slider brush, TMP_InputField color)
        {
            modal = page;
            canvas = drawingCanvas;
            preview = modelPreview;
            status = feedback;
            zoomSlider = size;
            rotationSlider = angle;
            brushSizeSlider = brush;
            colorHex = color;
            positionX = x;
            positionY = y;
            livePreview = true;
            size.onValueChanged.AddListener(SetScale);
            angle.onValueChanged.AddListener(SetRotation);
            x.onValueChanged.AddListener(value => { offset.x = value; InvalidateAlignment(); });
            y.onValueChanged.AddListener(value => { offset.y = value; InvalidateAlignment(); });
        }

        public void Open(bool inRoom)
        {
            connected = inRoom;
            if (applyLabel != null) applyLabel.text = inRoom ? "Áp dụng cho cả phòng" : "Lưu và chia sẻ khi vào phòng";
            modal.SetActive(true);
            if (livePreview && DogFaceWebBridge.Instance != null)
            {
                AttachPicker(DogFaceWebBridge.Instance);
                DogFaceWebBridge.Instance.SetImageDropEnabled(true);
            }
            if (livePreview) { preview?.Rotate(180f); mode = Mode.Align; }
            else preview?.ResetRotation();
            if (source == null && DogFaceStorage.TryLoad(out var saved)) LoadImageBytes(saved, true);
            dirty = true;
            RenderIfDirty();
            Opened?.Invoke();
        }

        public void Close()
        {
            activeStroke = null;
            if (livePreview) DogFaceWebBridge.Instance?.SetImageDropEnabled(false);
            DetachPicker();
            modal.SetActive(false);
            Closed?.Invoke();
        }

        public void ChooseImage()
        {
            var bridge = DogFaceWebBridge.Instance;
            if (bridge == null) { SetStatus("Image picker is unavailable."); return; }
            AttachPicker(bridge);
            bridge.OpenPicker();
        }

        private void AttachPicker(DogFaceWebBridge bridge)
        {
            bridge.ImagePicked -= OnPicked;
            bridge.PickerFailed -= OnPickerFailed;
            bridge.ImagePicked += OnPicked;
            bridge.PickerFailed += OnPickerFailed;
        }

        public void SetAlignMode() => mode = Mode.Align;
        public void SetDrawMode() => mode = Mode.Draw;
        public void SetEraseMode() => mode = Mode.Erase;
        public void Undo() { if (strokes.Count > 0) { strokes.RemoveAt(strokes.Count - 1); dirty = true; } }
        public void ClearDrawing() { strokes.Clear(); dirty = true; }

        public void ResetAlignment()
        {
            offset = Vector2.zero;
            rotation = 0;
            scale = SourceCoverScale();
            if (zoomSlider != null) zoomSlider.SetValueWithoutNotify(scale);
            if (rotationSlider != null) rotationSlider.SetValueWithoutNotify(0);
            SyncOffsetSliders();
            InvalidateAlignment();
        }

        public void Apply()
        {
            RenderIfDirty();
            if (composite == null) { SetStatus("Add a photo or drawing first."); return; }
            foreach (var quality in new[] { 88, 82, 76, 70, 64, 58, 52, 46 })
            {
                var jpeg = ImageConversion.EncodeToJPG(composite, quality);
                if (!DogFaceImageValidator.IsValidFaceJpeg(jpeg, out _)) continue;
                var saved = DogFaceStorage.Save(jpeg);
                preview?.ApplyFaceJpeg(jpeg);
                Applied?.Invoke(jpeg);
                SetStatus(saved
                    ? (connected ? "Đã lưu; đang chia sẻ với phòng." : "Đã lưu trên thiết bị.")
                    : "Đã áp dụng trong phiên này nhưng chưa lưu được trên thiết bị.");
                Close();
                return;
            }
            SetStatus("Could not encode this face below the 128 KiB network limit.");
        }

        public void UseDefault()
        {
            DogFaceStorage.Delete();
            if (source != null) Destroy(source);
            source = null;
            ClearDrawing();
            ResetAlignment();
            DefaultRequested?.Invoke();
            SetStatus("Default face selected.");
            Close();
        }

        private void OnPicked(byte[] bytes)
        {
            if (!livePreview) DetachPicker();
            if (!LoadImageBytes(bytes, false)) SetStatus("Unsupported, damaged, or oversized image.");
        }

        private void OnPickerFailed(string message)
        {
            if (!livePreview) DetachPicker();
            SetStatus(message);
        }

        private void DetachPicker()
        {
            var bridge = DogFaceWebBridge.Instance;
            if (bridge == null) return;
            bridge.ImagePicked -= OnPicked;
            bridge.PickerFailed -= OnPickerFailed;
        }

        private bool LoadImageBytes(byte[] bytes, bool confirmedJpeg)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > CustomDogFaceConstants.MaximumSourceBytes) return false;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp };
            if (!ImageConversion.LoadImage(texture, bytes, false)
                || texture.width > CustomDogFaceConstants.MaximumSourceDimension
                || texture.height > CustomDogFaceConstants.MaximumSourceDimension
                || (long)texture.width * texture.height > CustomDogFaceConstants.MaximumSourcePixels)
            { Destroy(texture); return false; }
            if (source != null) Destroy(source);
            source = texture;
            ResetAlignment();
            if (!confirmedJpeg) SetStatus($"Loaded {texture.width} × {texture.height}. Drag, zoom, rotate, or draw.");
            return true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (mode == Mode.Align) return;
            activeStroke = new Stroke { color = ReadColor(), size = ReadBrushSize(), erase = mode == Mode.Erase };
            activeStroke.points.Add(ToUv(eventData.position));
            strokes.Add(activeStroke);
            while (strokes.Count > CustomDogFaceConstants.MaximumUndoStrokes) strokes.RemoveAt(0);
            dirty = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (mode == Mode.Align)
            {
                offset += ToUv(eventData.position) - ToUv(eventData.position - eventData.delta);
                if (livePreview)
                {
                    offset = new Vector2(Mathf.Clamp(offset.x, -1, 1), Mathf.Clamp(offset.y, -1, 1));
                    SyncOffsetSliders();
                }
                InvalidateAlignment();
            }
            else if (activeStroke != null) activeStroke.points.Add(ToUv(eventData.position));
            dirty = true;
        }

        public void OnPointerUp(PointerEventData eventData) => activeStroke = null;
        public void OnScroll(PointerEventData eventData)
        {
            SetScale(scale * Mathf.Pow(1.08f, eventData.scrollDelta.y));
            zoomSlider?.SetValueWithoutNotify(scale);
        }
        private void SetScale(float value) { scale = Mathf.Clamp(value, .1f, 8f); InvalidateAlignment(); }
        private void SetRotation(float value) { rotation = Mathf.Clamp(value, -180, 180); InvalidateAlignment(); }
        private void InvalidateAlignment() { alignmentDirty = true; dirty = true; }

        private void SyncOffsetSliders()
        {
            positionX?.SetValueWithoutNotify(offset.x);
            positionY?.SetValueWithoutNotify(offset.y);
        }

        private Vector2 ToUv(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.rectTransform, screen, null, out var local);
            var rect = canvas.rectTransform.rect;
            return new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, local.x), Mathf.InverseLerp(rect.yMin, rect.yMax, local.y));
        }

        private float SourceCoverScale()
        {
            return 1f;
        }

        private Color ReadColor()
        {
            var value = colorHex != null ? colorHex.text : "#ffffff";
            return ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.white;
        }

        private float ReadBrushSize() => Mathf.Clamp(brushSizeSlider != null ? brushSizeSlider.value : .025f, .003f, .2f);

        private void LateUpdate() => RenderIfDirty();

        private void RenderIfDirty()
        {
            if (!dirty || !IsOpen) return;
            dirty = false;
            if (composite == null)
            {
                composite = new Texture2D(CustomDogFaceConstants.TextureSize, CustomDogFaceConstants.TextureSize,
                    TextureFormat.RGB24, false, false) { name = "Dog Face Composite", wrapMode = TextureWrapMode.Clamp };
            }
            const int size = CustomDogFaceConstants.TextureSize;
            basePixels ??= new Color[size * size];
            pixels ??= new Color[size * size];
            if (alignmentDirty)
            {
                RenderBase(size);
                alignmentDirty = false;
            }
            Array.Copy(basePixels, pixels, pixels.Length);
            foreach (var stroke in strokes) DrawStroke(pixels, basePixels, size, stroke);
            composite.SetPixels(pixels);
            composite.Apply(false, false);
            canvas.texture = composite;
            if (livePreview && (source != null || strokes.Count > 0)) preview?.FaceSurface?.Apply(composite);
            else if (livePreview) preview?.ResetFaceToDefault();
        }

        private void RenderBase(int size)
        {
            var background = new Color(.77f, .58f, .35f, 1);
            if (source == null)
            {
                Array.Fill(basePixels, background);
                return;
            }
            var radians = -rotation * Mathf.Deg2Rad;
            var sine = Mathf.Sin(radians);
            var cosine = Mathf.Cos(radians);
            var aspect = (float)source.width / source.height;
            var center = Vector2.one * .5f + offset;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var p = new Vector2((x + .5f) / size, (y + .5f) / size) - center;
                p = new Vector2(p.x * cosine - p.y * sine, p.x * sine + p.y * cosine) / scale;
                if (aspect >= 1f) p.x /= aspect;
                else p.y *= aspect;
                var sampleUv = p + Vector2.one * .5f;
                var color = background;
                if (sampleUv.x is >= 0 and <= 1 && sampleUv.y is >= 0 and <= 1)
                {
                    var sampled = source.GetPixelBilinear(sampleUv.x, sampleUv.y);
                    color = Color.Lerp(background, sampled, sampled.a);
                }
                basePixels[y * size + x] = color;
            }
        }

        private static void DrawStroke(Color[] pixels, Color[] basePixels, int size, Stroke stroke)
        {
            if (stroke.points.Count == 0) return;
            for (var point = 0; point < stroke.points.Count; point++)
            {
                var a = point == 0 ? stroke.points[point] : stroke.points[point - 1];
                var b = stroke.points[point];
                var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * size * 2));
                for (var step = 0; step <= steps; step++)
                {
                    var uv = Vector2.Lerp(a, b, (float)step / steps);
                    var radius = Mathf.Max(1, Mathf.RoundToInt(stroke.size * size));
                    var cx = Mathf.RoundToInt(uv.x * (size - 1));
                    var cy = Mathf.RoundToInt(uv.y * (size - 1));
                    for (var y = -radius; y <= radius; y++)
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y > radius * radius) continue;
                        var px = cx + x; var py = cy + y;
                        if (px < 0 || py < 0 || px >= size || py >= size) continue;
                        var pixelIndex = py * size + px;
                        pixels[pixelIndex] = stroke.erase ? basePixels[pixelIndex] : stroke.color;
                    }
                }
            }
        }

        private void SetStatus(string value) { if (status != null) status.text = value; }

        private void OnDestroy()
        {
            if (livePreview) DogFaceWebBridge.Instance?.SetImageDropEnabled(false);
            DetachPicker();
            if (source != null) Destroy(source);
            if (composite != null) Destroy(composite);
        }
    }
}
