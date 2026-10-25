using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The player-facing texts the designer edits in <c>Assets/_Game/Texts/Yazilar.xlsx</c>, as imported by the editor
    /// pipeline into <c>Resources/GameTextTable</c>: parallel key / text arrays (an empty text means "use the code
    /// default"). Read through <see cref="Texts"/>; never edited by hand.
    /// </summary>
    public sealed class GameTextTable : ScriptableObject
    {
        [Tooltip("Text ids (Excel column 'Anahtar'), same order as Values.")]
        [SerializeField] private string[] _keys = new string[0];
        [Tooltip("Texts the game shows (Excel column 'Metin'); empty = the code default.")]
        [SerializeField] private string[] _values = new string[0];

        public string[] Keys => _keys;
        public string[] Values => _values;

        /// <summary>Replaces the rows (editor import, tests). Arrays must have the same length.</summary>
        public void SetRows(string[] keys, string[] values)
        {
            _keys = keys ?? new string[0];
            _values = values ?? new string[0];
        }
    }
}
