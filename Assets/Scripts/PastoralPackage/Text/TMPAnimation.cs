namespace Pastoral.UI{
    using TMPro;
    using UnityEngine;
    using System.Collections;

    public static class TMPAnimation {
        public static IEnumerator TextBounceAnimationUpdate(TextMeshProUGUI textMeshProUGUI, string text) {
            int CurrentCharacterIndex = 0;
            bool LowerWave = false;
            textMeshProUGUI.text = text;
            while (textMeshProUGUI.text == text)
            {
                yield return new WaitForFixedUpdate();
                TextBounceAnimation(textMeshProUGUI, ref CurrentCharacterIndex, ref LowerWave);
            }
        }
        private static void TextBounceAnimation(TextMeshProUGUI textMeshProUGUI, ref int CurrentCharacterIndex,ref bool LowerWave){
            void AddToCurrentWordIndex(int maxLength, ref int CurrentCharacterIndex){
                CurrentCharacterIndex++;
                if(CurrentCharacterIndex >= maxLength){
                    CurrentCharacterIndex = 0;
                    // LowerWave = !LowerWave;
                }
            }
            textMeshProUGUI.ForceMeshUpdate();
            var textInfo = textMeshProUGUI.textInfo;
            {
                var charInfo = textInfo.characterInfo[CurrentCharacterIndex];
                
                if (!charInfo.isVisible){
                    AddToCurrentWordIndex(textInfo.characterInfo.Length, ref CurrentCharacterIndex);
                    return;
                }
                if (textInfo.meshInfo[charInfo.materialReferenceIndex].vertexCount == 0){
                    AddToCurrentWordIndex(textInfo.characterInfo.Length, ref CurrentCharacterIndex);
                    return;
                }
                var vertics = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;
                for (int j = 0; j < 4; j++)
                {
                    var originalVerts = vertics[charInfo.vertexIndex + j];
                    float sinValue =  Mathf.Sin(Time.time*5);
                    vertics[charInfo.vertexIndex + j] = originalVerts+ new Vector3(0,Mathf.Abs(sinValue)*40);
                    if(LowerWave){
                        if(sinValue < 0f){
                            LowerWave = false;
                            AddToCurrentWordIndex(textInfo.characterInfo.Length, ref CurrentCharacterIndex);
                            return;
                        }
                    }
                    else{
                        if(sinValue > 0f){
                            LowerWave = true;
                            AddToCurrentWordIndex(textInfo.characterInfo.Length, ref CurrentCharacterIndex);
                            return;
                        }
                    }
                }
            }

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                var meshInfo = textInfo.meshInfo;
                meshInfo[i].mesh.vertices = meshInfo[i].vertices;
                textMeshProUGUI.UpdateGeometry(meshInfo[i].mesh, i);
            }
        }
    }
}