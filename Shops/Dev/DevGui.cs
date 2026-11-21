using UnityEngine;

namespace POGLib.Shops.Dev
{
    /// <summary>
    /// Minimal immediate-mode layout over GUI (not GUILayout: its params-array overloads are awkward
    /// through Il2CppInterop). A cursor walks down a column; each call takes one row.
    /// </summary>
    internal sealed class DevGui
    {
        public const float RowH = 22f;
        private float _x, _y, _w;

        public void Begin(float x, float y, float w) { _x = x; _y = y; _w = w; }
        public float Y => _y;
        public void Space(float h = 6f) => _y += h;

        public void Label(string text, float h = RowH)
        {
            GUI.Label(new Rect(_x, _y, _w, h), text);
            _y += h;
        }

        public bool Button(string text, float h = RowH)
        {
            bool hit = GUI.Button(new Rect(_x, _y, _w, h - 2), text);
            _y += h;
            return hit;
        }

        /// <summary>Several buttons side by side; returns the index clicked or -1.</summary>
        public int Buttons(params string[] texts)
        {
            int hit = -1;
            float bw = (_w - 4 * (texts.Length - 1)) / texts.Length;
            for (int i = 0; i < texts.Length; i++)
                if (GUI.Button(new Rect(_x + i * (bw + 4), _y, bw, RowH - 2), texts[i])) hit = i;
            _y += RowH;
            return hit;
        }

        /// <summary>A label with buttons at the right end of the row; returns the button index clicked or -1.</summary>
        public int LabelWithButtons(string text, params string[] buttons)
        {
            const float bw = 34f;
            float labelW = _w - buttons.Length * (bw + 2);
            GUI.Label(new Rect(_x, _y, labelW, RowH), text);
            int hit = -1;
            for (int i = 0; i < buttons.Length; i++)
                if (GUI.Button(new Rect(_x + labelW + i * (bw + 2), _y, bw, RowH - 2), buttons[i])) hit = i;
            _y += RowH;
            return hit;
        }

        /// <summary>
        /// A text box without GUI.TextField: the panel stopped drawing at its first GUI.TextField (2026-11-12;
        /// the exception was not logged then — probably a method stripped from this IL2CPP build, not verified).
        /// Clicking it takes the focus; ShopDevTools feeds typed characters to the focused box.
        /// </summary>
        public void TextBox(string id, string label, string value, ref string focused, float labelW = 90f)
        {
            GUI.Label(new Rect(_x, _y, labelW, RowH), label);
            bool has = focused == id;
            if (GUI.Button(new Rect(_x + labelW, _y, _w - labelW, RowH - 2), (value ?? "") + (has ? "_" : ""))) focused = has ? null : id;
            _y += RowH;
        }

        /// <summary>Apply this frame's typed characters to a string (Backspace deletes, Enter ends).</summary>
        public static string Type(string value, string typed, out bool done)
        {
            done = false;
            var sb = new System.Text.StringBuilder(value ?? "");
            foreach (char c in typed ?? "")
            {
                if (c == '\b') { if (sb.Length > 0) sb.Length--; }
                else if (c == '\n' || c == '\r') done = true;
                else if (!char.IsControl(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        public void Box(Rect r, string title) => GUI.Box(r, title);
    }
}
