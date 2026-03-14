using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RTLTMPro
{
    public static class RTLUGUIContextMenu
    {
        private const string kStandardSpritePath = "UI/Skin/UISprite.psd";
        private const string kInputFieldBackgroundPath = "UI/Skin/InputFieldBackground.psd";

        [MenuItem("GameObject/UI/Input Field - RTL UGUI", false, 2038)]
        private static void AddRTLUGUIInputField(MenuCommand menuCommand)
        {
            GameObject go = CreateInputField();
            PlaceUIElementRoot(go, menuCommand);
        }

        private static GameObject CreateInputField()
        {
            GameObject root = new GameObject("InputField - RTL UGUI", typeof(RectTransform), typeof(Image), typeof(RTLInputField));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(160f, 30f);

            Image image = root.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(kInputFieldBackgroundPath);
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            RTLInputField inputField = root.GetComponent<RTLInputField>();

            GameObject textArea = CreateUIObject("Text Area", root);
            GameObject placeholder = CreateUIObject("Placeholder", textArea);
            GameObject text = CreateUIObject("Text", textArea);

            textArea.AddComponent<RectMask2D>();

            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.sizeDelta = Vector2.zero;
            textAreaRect.offsetMin = new Vector2(10f, 6f);
            textAreaRect.offsetMax = new Vector2(-10f, -7f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Text textComponent = text.AddComponent<Text>();
            textComponent.font = font;
            textComponent.supportRichText = true;
            textComponent.alignment = TextAnchor.MiddleRight;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Truncate;
            textComponent.color = new Color32(50, 50, 50, 255);

            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text placeholderComponent = placeholder.AddComponent<Text>();
            placeholderComponent.font = font;
            placeholderComponent.text = "Enter Text...";
            placeholderComponent.alignment = TextAnchor.MiddleRight;
            placeholderComponent.fontStyle = FontStyle.Italic;
            placeholderComponent.color = new Color32(50, 50, 50, 128);

            RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            inputField.textComponent = textComponent;
            inputField.placeholder = placeholderComponent;

            return root;
        }

        private static GameObject CreateUIObject(string name, GameObject parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            GameObjectUtility.SetParentAndAlign(go, parent);
            return go;
        }

        private static void PlaceUIElementRoot(GameObject element, MenuCommand menuCommand)
        {
            GameObject parent = menuCommand.context as GameObject;
            if (parent == null || parent.GetComponentInParent<Canvas>() == null)
                parent = ContextMenu.GetOrCreateCanvasGameObject();

            string uniqueName = GameObjectUtility.GetUniqueNameForSibling(parent.transform, element.name);
            element.name = uniqueName;
            Undo.RegisterCreatedObjectUndo(element, "Create " + element.name);
            Undo.SetTransformParent(element.transform, parent.transform, "Parent " + element.name);
            GameObjectUtility.SetParentAndAlign(element, parent);
            Selection.activeGameObject = element;

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }
        }
    }
}
