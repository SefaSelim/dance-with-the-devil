using UnityEngine;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Block-art Defne portrait (placeholder until the team's drawing arrives), built from UI blocks at any scale.
    /// Shows the contract costs that read on a face (MAIN_GDD §8): hair loses its colour (1 "Rengin"), fringe,
    /// glasses and a side lock go (4 "Yüzün"), everything greys (6 "Kendin"); and can crumble block by block
    /// (always-yes ending, §11 "Gri kabuk dağılır"). Used by the HUD (small) and the ending screen (large).
    /// Plain class: the owner calls <see cref="TickCrumble"/> on unscaled time. No per-frame allocations.
    /// </summary>
    public sealed class DefnePortrait
    {
        private enum Role { Skin, Hair, FacePart, Clothes, Detail }

        private struct Part
        {
            public string Name;
            public Role Role;
            public Color Color;
            public Vector2 Position;
            public Vector2 Size;

            public Part(string name, Role role, Color color, float x, float y, float w, float h)
            {
                Name = name;
                Role = role;
                Color = color;
                Position = new Vector2(x, y);
                Size = new Vector2(w, h);
            }
        }

        private static readonly Color HairColor = new Color(0.45f, 0.18f, 0.4f, 1f);
        private static readonly Color HairDark = new Color(0.33f, 0.12f, 0.3f, 1f);
        private static readonly Color SkinColor = new Color(0.95f, 0.78f, 0.62f, 1f);
        private static readonly Color OverallsColor = new Color(0.1f, 0.35f, 0.4f, 1f);
        private static readonly Color LensColor = new Color(0.78f, 0.86f, 0.9f, 1f);

        // 36×36 design grid centred on (0, 0). Draw order = list order. Face parts lost to contract 4 are listed in
        // the same order as DefneCostVisuals' default (fringe, glasses, a side lock).
        private static readonly Part[] Design =
        {
            new Part("Hair_Back", Role.Hair, HairDark, 0f, 2f, 32f, 26f),
            new Part("Neck", Role.Skin, SkinColor, 0f, -13f, 8f, 4f),
            new Part("Overalls", Role.Clothes, OverallsColor, 0f, -16f, 28f, 4f),
            new Part("Strap_L", Role.Detail, UiKit.Turquoise, -7f, -16f, 2f, 4f),
            new Part("Strap_R", Role.Detail, UiKit.Turquoise, 7f, -16f, 2f, 4f),
            new Part("Face", Role.Skin, SkinColor, 0f, -3f, 20f, 18f),
            new Part("Hair_Top", Role.Hair, HairColor, 0f, 9f, 28f, 8f),
            new Part("Hair_Curl_L", Role.Hair, HairColor, -10f, 13f, 6f, 4f),
            new Part("Hair_Curl_R", Role.Hair, HairColor, 9f, 14f, 6f, 4f),
            new Part("Hair_R", Role.Hair, HairColor, 12f, -1f, 6f, 18f),
            new Part("Hair_L", Role.FacePart, HairColor, -12f, -1f, 6f, 18f),
            new Part("Hair_Fringe", Role.FacePart, HairColor, -3f, 4f, 14f, 4f),
            new Part("Glasses_L", Role.FacePart, UiKit.Ink, -5f, -3f, 8f, 6f),
            new Part("Glasses_R", Role.FacePart, UiKit.Ink, 5f, -3f, 8f, 6f),
            new Part("Lens_L", Role.FacePart, LensColor, -5f, -3f, 4f, 2f),
            new Part("Lens_R", Role.FacePart, LensColor, 5f, -3f, 4f, 2f),
            new Part("Mouth", Role.Detail, new Color(0.55f, 0.25f, 0.25f, 1f), 0f, -9f, 6f, 2f),
        };

        private readonly Image[] _images;
        private readonly RectTransform[] _rects;
        private readonly Vector2[] _basePositions;
        private readonly float _scale;

        // Crumble state.
        private readonly Vector2[] _velocity;
        private readonly float[] _delay;
        private readonly float[] _spin;
        private readonly float[] _angle;
        private float _crumbleTime;
        private bool _crumbling;

        private bool _hairGrey, _faceGone, _allGrey;

        /// <summary>Builds the portrait under <paramref name="parent"/>, centred, design units × <paramref name="scale"/>.</summary>
        public DefnePortrait(Transform parent, float scale)
        {
            _scale = Mathf.Max(0.1f, scale);
            int n = Design.Length;
            _images = new Image[n];
            _rects = new RectTransform[n];
            _basePositions = new Vector2[n];
            _velocity = new Vector2[n];
            _delay = new float[n];
            _spin = new float[n];
            _angle = new float[n];
            Vector2 mid = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < n; i++)
            {
                Part p = Design[i];
                _images[i] = UiKit.Box(parent, p.Name, p.Color, mid, mid, p.Position * _scale, p.Size * _scale);
                _rects[i] = _images[i].rectTransform;
                _basePositions[i] = p.Position * _scale;
            }
        }

        public bool IsCrumbling => _crumbling;

        /// <summary>Shows the costs of the first <paramref name="dealsTaken"/> contracts (1 hair, 4 face, 6 grey).</summary>
        public void ApplyCosts(int dealsTaken)
        {
            _hairGrey = dealsTaken >= 1;
            _faceGone = dealsTaken >= 4;
            _allGrey = dealsTaken >= 6;
            Refresh();
        }

        /// <summary>Everything grey (the always-yes shell), regardless of the costs.</summary>
        public void SetAllGrey()
        {
            _allGrey = true;
            Refresh();
        }

        /// <summary>Starts the crumble: blocks drop off one by one after a short, random delay.</summary>
        public void StartCrumble(float spread = 1.2f)
        {
            _crumbling = true;
            _crumbleTime = 0f;
            for (int i = 0; i < _images.Length; i++)
            {
                _delay[i] = Random.Range(0f, spread);
                _velocity[i] = new Vector2(Random.Range(-25f, 25f), Random.Range(10f, 45f)) * _scale;
                _spin[i] = Random.Range(-240f, 240f);
                _angle[i] = 0f;
            }
        }

        /// <summary>Advances the crumble (unscaled seconds). Returns true while blocks are still falling.</summary>
        public bool TickCrumble(float dt)
        {
            if (!_crumbling) return false;
            _crumbleTime += dt;
            float gravity = 260f * _scale;
            bool any = false;
            for (int i = 0; i < _images.Length; i++)
            {
                if (!_images[i].enabled) continue;
                float t = _crumbleTime - _delay[i];
                if (t <= 0f)
                {
                    any = true;
                    continue;
                }
                Vector2 v = _velocity[i];
                _rects[i].anchoredPosition = _basePositions[i] + new Vector2(v.x * t, v.y * t - 0.5f * gravity * t * t);
                _angle[i] += _spin[i] * dt;
                _rects[i].localRotation = Quaternion.Euler(0f, 0f, _angle[i]);
                Color c = _images[i].color;
                c.a = Mathf.Clamp01(1f - (t - 0.5f) / 0.6f);
                _images[i].color = c;
                if (c.a <= 0f) _images[i].enabled = false;
                else any = true;
            }
            return any;
        }

        private void Refresh()
        {
            for (int i = 0; i < _images.Length; i++)
            {
                Part p = Design[i];
                bool visible = !(_faceGone && p.Role == Role.FacePart);
                _images[i].enabled = visible;
                bool grey = _allGrey || (_hairGrey && (p.Role == Role.Hair || (p.Role == Role.FacePart && p.Name.StartsWith("Hair_"))));
                _images[i].color = grey ? Grey(p.Color) : p.Color;
            }
        }

        private static Color Grey(Color c)
        {
            float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) * 0.9f;
            return new Color(lum, lum, lum, c.a);
        }
    }
}
