using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnocksFromBeneath
{
// L-02: подписи, вписанные прямо в сцену (кнопки меню, заставки), переводятся один раз после загрузки сцены.
// Текст, который код выставляет сам, проходит через Loc.T в месте выставления, сюда не попадает:
// трогаются только TMP-тексты, чей текст — русский ключ таблицы.
public static class LocSceneTexts
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize() => SceneManager.sceneLoaded += (scene, mode) => Localize(scene);

    public static void Localize(Scene scene)
    {
        if (Loc.Current == Loc.SourceLanguage) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                if (!string.IsNullOrEmpty(text.text) && Loc.HasTranslation(text.text.Trim()))
                    text.text = Loc.T(text.text.Trim());
    }
}
}
