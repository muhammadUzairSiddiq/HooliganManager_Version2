using System;
using System.Collections;
using Pastoral;
using Pastoral.UI;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Pastoral.UI
{
    public class NavTabMenu : MonoBehaviour
    {
        [SerializeField] private Transform NavBtnSelectionPanel;
        [SerializeField] private RectTransform LeaguePanelParent;
        [SerializeField] private RectTransform[] BoardPanels;
        void Awake()
        {
            LoadBoards();
        }
        private void OnEnable()
        {
            StartCoroutine(UpdateLeagueData(BoardPanels[ActivedPanelID]));
        }
        private void LoadBoards()
        {
            navBtnLayoutElements = new LayoutElement[NavBtnSelectionPanel.childCount];
            for (int i = 0; i < NavBtnSelectionPanel.childCount; i++)
            {
                int index = i;
                Transform buttonTrans = NavBtnSelectionPanel.GetChild(i);
                navBtnLayoutElements[i] = buttonTrans.GetComponent<LayoutElement>();
                buttonTrans.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (index == ActivedPanelID) return;
                    StopAllCoroutines();
                    StartCoroutine(ActivateLeaguePanel(index));
                    StartCoroutine(ActivateLeagueBtn(index));
                });
            }
        }
        private LayoutElement[] navBtnLayoutElements;
        [SerializeField] private Color[] BtnToggleColor;
        private IEnumerator ActivateLeagueBtn(int id)
        {
            LayoutElement[] animateNavBtnLayoutElements = new LayoutElement[2];
            Button[] buttons = new Button[2];
            animateNavBtnLayoutElements[0] = navBtnLayoutElements[id];
            for (int i = 0; i < navBtnLayoutElements.Length; i++)
            {
                buttons[i] = navBtnLayoutElements[i].GetComponent<Button>();
                buttons[i].interactable = false;
                if (navBtnLayoutElements[i].flexibleWidth != 1)
                    animateNavBtnLayoutElements[1] = navBtnLayoutElements[i];
            }

            animateNavBtnLayoutElements[0].transform.GetChild(0).GetComponent<TextMeshProUGUI>().color = Color.black;
            animateNavBtnLayoutElements[1].transform.GetChild(0).GetComponent<TextMeshProUGUI>().color = Color.grey;
            Image[] animateNavBtnImage = new Image[2];
            animateNavBtnImage[0] = animateNavBtnLayoutElements[0].GetComponent<Image>();
            animateNavBtnImage[1] = animateNavBtnLayoutElements[1].GetComponent<Image>();
            float t = 0;
            while (t < 1)
            {
                t += Time.deltaTime * 4;
                animateNavBtnImage[0].color = Color.Lerp(BtnToggleColor[1], BtnToggleColor[0], t);
                animateNavBtnImage[1].color = Color.Lerp(BtnToggleColor[0], BtnToggleColor[1], t);
                animateNavBtnLayoutElements[0].flexibleWidth = Mathf.Lerp(1, 1.5f, t);
                animateNavBtnLayoutElements[1].flexibleWidth = Mathf.Lerp(1.5f, 1, t);
                yield return new WaitForEndOfFrame();
            }
            yield return new WaitForSeconds(0.1f);
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].interactable = true;
        }
        private int ActivedPanelID = 0;
        private IEnumerator ActivateLeaguePanel(int activePanelID)
        {
            int movementXDir = activePanelID > ActivedPanelID ? 1 : -1;
            Debug.Log("activePanelID: " + activePanelID);
            BoardPanels[activePanelID].gameObject.SetActive(true);
            UIp.UITweeningInsideScreenViewFrom(this, BoardPanels[activePanelID], LeaguePanelParent, new Vector2(movementXDir, 0), 4);
            if (ActivedPanelID != -1)
            {
                GameObject LeaguePanelObject = BoardPanels[ActivedPanelID].gameObject;
                yield return UIp.UITweeningOutsideScreenViewFrom(this, BoardPanels[ActivedPanelID], LeaguePanelParent, new Vector2(-movementXDir, 0), 4, action: () => LeaguePanelObject.SetActive(false));
            }
            ActivedPanelID = activePanelID;
            // if(leagueData != null)
            StartCoroutine(UpdateLeagueData(BoardPanels[ActivedPanelID]));
        }

        private IEnumerator UpdateLeagueData(RectTransform leagueDisplayPanel)
        {
            leagueDisplayPanel.gameObject.SetActive(true);
            yield return new WaitForSeconds(3f);
            float WaitForSeconds = 0.7f;
            yield return new WaitForSecondsRealtime(WaitForSeconds);
        }
    }
}