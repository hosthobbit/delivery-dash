using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DeliveryDash
{
    /// <summary>Small helpers for building the uGUI interface in code with a consistent look.</summary>
    public static class UiKit
    {
        public static Font Title, Bold, Black;
        public static Sprite Rounded, RoundedSmall, Circle;

        public static readonly Color Ink = new Color(0.13f, 0.11f, 0.2f);
        public static readonly Color Cream = new Color(1f, 0.97f, 0.9f);
        public static readonly Color Sky = new Color(0.36f, 0.66f, 0.95f);
        public static readonly Color Sun = new Color(1f, 0.78f, 0.25f);
        public static readonly Color Tomato = new Color(0.94f, 0.33f, 0.27f);
        public static readonly Color Leaf = new Color(0.33f, 0.74f, 0.42f);
        public static readonly Color Panel = new Color(1f, 0.97f, 0.9f, 0.97f);
        public static readonly Color Shade = new Color(0.08f, 0.06f, 0.14f, 0.55f);

        public static void Init()
        {
            if (Title) return;
            Title = Resources.Load<Font>("Fonts/LilitaOne");
            Bold = Resources.Load<Font>("Fonts/NotoSansBold");
            Black = Resources.Load<Font>("Fonts/NotoSansBlack");
            Rounded = MakeRounded(64, 22);
            RoundedSmall = MakeRounded(32, 10);
            Circle = MakeRounded(64, 32);
        }

        /// <summary>A white rounded-rectangle sprite with 9-slice borders, drawn once at start-up.</summary>
        static Sprite MakeRounded(int size, int radius)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform r, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.offsetMin = offsetMin; r.offsetMax = offsetMax;
            return r;
        }

        public static RectTransform Center(this RectTransform r, Vector2 size, Vector2 pos = default)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = pos;
            return r;
        }

        public static RectTransform Fill(this RectTransform r) => r.Place(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static Image Box(Transform parent, string name, Color colour, Sprite sprite = null)
        {
            var r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite ? sprite : Rounded;
            img.type = Image.Type.Sliced;
            img.color = colour;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color colour, Font font = null, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var r = Rect("Text", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = font ? font : Bold;
            t.fontSize = size;
            t.color = colour;
            t.alignment = align;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Outline Outlined(this Text t, Color c, float d = 2f)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = c; o.effectDistance = new Vector2(d, -d);
            return o;
        }

        public static Button Button(Transform parent, string text, Color colour, UnityAction onClick, int fontSize = 34, Color? textColour = null)
        {
            var img = Box(parent, text + " Button", colour);
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            cb.selectedColor = new Color(1.12f, 1.12f, 1.12f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            cb.colorMultiplier = 1.15f;
            b.colors = cb;
            var label = Label(img.transform, text, fontSize, textColour ?? Ink, Black);
            label.rectTransform.Fill();
            b.onClick.AddListener(() => Sfx.Play(Sfx.Kind.Click, 0.5f));
            b.onClick.AddListener(onClick);
            // a bold outline shows which button keyboard / gamepad focus is on
            var focus = Box(img.transform, "Focus", Sun, Rounded);
            focus.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(-6, -6), new Vector2(6, 6));
            focus.transform.SetAsFirstSibling();
            focus.gameObject.AddComponent<FocusRing>().Target = b;
            return b;
        }

        /// <summary>A vertical stack layout.</summary>
        public static VerticalLayoutGroup Stack(RectTransform r, float spacing, int padding = 0, TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = r.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = align;
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            return v;
        }

        public static LayoutElement Size(this Component c, float height, float width = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            if (width > 0) { le.preferredWidth = width; le.minWidth = width; }
            return le;
        }

        public static Slider Slider(Transform parent, float value, Action<float> onChange)
        {
            var root = Rect("Slider", parent);
            var s = root.gameObject.AddComponent<Slider>();
            var bg = Box(root, "Track", new Color(0.82f, 0.78f, 0.7f));
            bg.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -7), new Vector2(0, 7));
            var fillArea = Rect("Fill Area", root).Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -7), new Vector2(0, 7));
            var fill = Box(fillArea, "Fill", Sky);
            fill.rectTransform.Place(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handleArea = Rect("Handle Area", root).Place(Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-14, 0));
            var handle = Box(handleArea, "Handle", Cream, Circle);
            handle.rectTransform.sizeDelta = new Vector2(34, 34);
            var ring = handle.gameObject.AddComponent<Outline>(); ring.effectColor = Ink; ring.effectDistance = new Vector2(2, -2);
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = 0; s.maxValue = 1; s.value = value;
            s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }
    }
}
