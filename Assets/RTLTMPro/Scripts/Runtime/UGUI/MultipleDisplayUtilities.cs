using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    internal static class MultipleDisplayUtilities
    {
        public static bool GetRelativeMousePositionForDrag(PointerEventData eventData, ref Vector2 position)
        {
#if UNITY_EDITOR
            position = eventData.position;
#else
            int pressDisplayIndex = eventData.pointerPressRaycast.displayIndex;
            Vector3 relativePosition = RelativeMouseAtScaled(eventData.position, eventData.displayIndex);
            int currentDisplayIndex = (int)relativePosition.z;

            if (currentDisplayIndex != pressDisplayIndex)
                return false;

            position = pressDisplayIndex != 0 ? (Vector2)relativePosition : eventData.position;
#endif
            return true;
        }

        public static Vector3 RelativeMouseAtScaled(Vector2 position, int displayIndex)
        {
#if !UNITY_EDITOR && !UNITY_WSA
            Display display = Display.main;

#if ENABLE_INPUT_SYSTEM && PACKAGE_INPUTSYSTEM && UNITY_ANDROID
            display = Display.displays[displayIndex];
            if (!Screen.fullScreen)
                return new Vector3(position.x, position.y, displayIndex);
#endif
            if (display.renderingWidth != display.systemWidth || display.renderingHeight != display.systemHeight)
            {
                float systemAspectRatio = display.systemWidth / (float)display.systemHeight;
                Vector2 sizePlusPadding = new Vector2(display.renderingWidth, display.renderingHeight);
                Vector2 padding = Vector2.zero;

                if (Screen.fullScreen)
                {
                    float aspectRatio = Screen.width / (float)Screen.height;
                    if (display.systemHeight * aspectRatio < display.systemWidth)
                    {
                        sizePlusPadding.x = display.renderingHeight * systemAspectRatio;
                        padding.x = (sizePlusPadding.x - display.renderingWidth) * 0.5f;
                    }
                    else
                    {
                        sizePlusPadding.y = display.renderingWidth / systemAspectRatio;
                        padding.y = (sizePlusPadding.y - display.renderingHeight) * 0.5f;
                    }
                }

                Vector2 sizePlusPositivePadding = sizePlusPadding - padding;
                if (position.y < -padding.y || position.y > sizePlusPositivePadding.y ||
                    position.x < -padding.x || position.x > sizePlusPositivePadding.x)
                {
                    Vector2 adjustedPosition = position;
                    if (!Screen.fullScreen)
                    {
                        adjustedPosition.x -= (display.renderingWidth - display.systemWidth) * 0.5f;
                        adjustedPosition.y -= (display.renderingHeight - display.systemHeight) * 0.5f;
                    }
                    else
                    {
                        adjustedPosition += padding;
                        adjustedPosition.x *= display.systemWidth / sizePlusPadding.x;
                        adjustedPosition.y *= display.systemHeight / sizePlusPadding.y;
                    }

#if ENABLE_INPUT_SYSTEM && PACKAGE_INPUTSYSTEM && (UNITY_STANDALONE_WIN || UNITY_ANDROID)
                    Vector3 relativePos = new Vector3(adjustedPosition.x, adjustedPosition.y, displayIndex);
#else
                    Vector3 relativePos = Display.RelativeMouseAt(adjustedPosition);
#endif
                    if (relativePos.z != 0)
                        return relativePos;
                }

#if ENABLE_INPUT_SYSTEM && PACKAGE_INPUTSYSTEM && UNITY_ANDROID
                return new Vector3(position.x, position.y, displayIndex);
#else
                return new Vector3(position.x, position.y, 0);
#endif
            }
#endif
#if ENABLE_INPUT_SYSTEM && PACKAGE_INPUTSYSTEM && (UNITY_STANDALONE_WIN || UNITY_ANDROID)
            return new Vector3(position.x, position.y, displayIndex);
#else
            return Display.RelativeMouseAt(position);
#endif
        }
    }
}
