using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

/// <summary>
/// Proximity-based civilian conversation UI.  It owns one bottom conversation
/// panel for every pedestrian, so ambient NPCs do not create competing canvases.
/// </summary>
public sealed class NpcConversationUI : MonoBehaviour
{
    public static NpcConversationUI Instance { get; private set; }
    public const float InteractionRange = 7f;

    RectTransform frame;
    RectTransform questionBox, sendButton, voiceButton, closeButton, transcriptRect, voiceStateRect;
    GameObject nearbyRoot, conversationRoot;
    TextMeshProUGUI nearbyLabel, title, transcript, voiceState;
    TMP_InputField questionInput;
    GridLayoutGroup topics;
    SocialNpc nearby, current;
    AgentController speaker;
    float nextScan;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    DictationRecognizer dictation;
#endif

    public SocialNpc NearbyNpc => nearby;
    public SocialNpc CurrentNpc => current;
    public bool IsConversationOpen => conversationRoot && conversationRoot.activeSelf;
    public bool UsesFullSafeWidth => conversationRoot && frame && Mathf.Abs((conversationRoot.transform as RectTransform).rect.width-frame.rect.width)<1f;
    public string LastTranscript => transcript ? transcript.text : string.Empty;

    public static NpcConversationUI Ensure(RectTransform targetFrame)
    {
        if(Instance)
        {
            if(!Instance.frame)Instance.Build(targetFrame);
            return Instance;
        }
        var go=new GameObject("NpcConversationUI");
        Instance=go.AddComponent<NpcConversationUI>();
        Instance.Build(targetFrame);
        return Instance;
    }

    void Awake()
    {
        if(Instance && Instance!=this){Destroy(gameObject);return;}
        Instance=this;
    }

    void Build(RectTransform targetFrame)
    {
        frame=targetFrame;
        if(!frame)return;
        transform.SetParent(frame,false);

        var nearbyRect=LandscapeUI.Rect("NearbyTalk",frame,0,0,390,66);
        nearbyRect.anchorMin=nearbyRect.anchorMax=new Vector2(.5f,0f);
        nearbyRect.pivot=new Vector2(.5f,0f);
        nearbyRect.anchoredPosition=new Vector2(0,92);
        nearbyRoot=nearbyRect.gameObject;
        nearbyRoot.SetActive(false);
        var talk=LandscapeUI.Button("TalkButton",nearbyRect,"TALK",0,0,390,66,"green");
        LandscapeUI.Stretch(talk.transform as RectTransform);
        nearbyLabel=talk.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if(nearbyLabel){nearbyLabel.color=Color.white;nearbyLabel.fontSize=24f;nearbyLabel.fontSizeMin=18f;nearbyLabel.fontSizeMax=26f;}
        talk.onClick.AddListener(()=>{if(nearby)TryOpen(nearby);});

        var panel=LandscapeUI.Rect("ConversationPanel",frame,0,0,1600,304);
        panel.anchorMin=new Vector2(0,0);panel.anchorMax=new Vector2(1,0);panel.pivot=new Vector2(.5f,0);
        panel.anchoredPosition=new Vector2(0,0);panel.sizeDelta=new Vector2(0,304);
        var background=panel.gameObject.AddComponent<Image>();
        background.color=new Color(.035f,.055f,.068f,.98f);background.raycastTarget=true;
        // Live minimap and camera widgets use their own canvases; keep the active
        // conversation above them so every prompt and button remains readable/clickable.
        var overlayCanvas=panel.gameObject.AddComponent<Canvas>();
        overlayCanvas.overrideSorting=true;overlayCanvas.sortingOrder=19000;
        panel.gameObject.AddComponent<GraphicRaycaster>();
        conversationRoot=panel.gameObject;
        var accent=LandscapeUI.Image("Accent",panel,0,0,1600,4,null,LandscapeUI.Green).rectTransform;
        accent.anchorMin=new Vector2(0,1);accent.anchorMax=new Vector2(1,1);accent.sizeDelta=new Vector2(0,4);
        title=LandscapeUI.Text("NpcName",panel,"LOCAL RESIDENT",24,14,430,34,25,null,true);
        transcript=LandscapeUI.Text("Transcript",panel,"",24,51,920,82,20,LandscapeUI.White,false);
        transcriptRect=transcript.rectTransform;
        transcript.overflowMode=TextOverflowModes.Truncate;
        voiceState=LandscapeUI.Text("VoiceState",panel,"ASK ABOUT THE CITY",976,18,560,28,16,LandscapeUI.Muted,false,TextAlignmentOptions.Right);
        voiceStateRect=voiceState.rectTransform;

        BuildQuestionBox(panel);
        BuildTopics(panel);
        conversationRoot.SetActive(false);
        nearbyRoot.SetActive(false);
    }

    void BuildQuestionBox(RectTransform panel)
    {
        var inputRect=LandscapeUI.Rect("QuestionBox",panel,24,144,936,52);questionBox=inputRect;
        var bg=inputRect.gameObject.AddComponent<Image>();bg.color=new Color(.10f,.14f,.16f,1f);bg.raycastTarget=true;
        inputRect.gameObject.AddComponent<RectMask2D>();
        questionInput=inputRect.gameObject.AddComponent<TMP_InputField>();
        var text=LandscapeUI.Text("Text",inputRect,"",14,7,902,38,20,LandscapeUI.White);
        var placeholder=LandscapeUI.Text("Placeholder",inputRect,"Ask about weather, the match, stadium, pubs or events...",14,7,902,38,18,LandscapeUI.Muted);
        questionInput.textViewport=inputRect;questionInput.textComponent=text;questionInput.placeholder=placeholder;
        questionInput.lineType=TMP_InputField.LineType.SingleLine;
        questionInput.onSubmit.AddListener(AskQuestion);

        var send=LandscapeUI.Button("SendQuestion",panel,"SEND",974,144,170,52,"green");
        sendButton=send.transform as RectTransform;
        send.onClick.AddListener(()=>AskQuestion(questionInput.text));
        var voice=LandscapeUI.Button("VoiceQuestion",panel,"MIC / VOICE",1158,144,194,52,"dark");
        voiceButton=voice.transform as RectTransform;
        voice.onClick.AddListener(StartVoiceInput);
        var close=LandscapeUI.Button("CloseConversation",panel,"CLOSE",1366,144,170,52,"dark");
        closeButton=close.transform as RectTransform;
        close.onClick.AddListener(Close);
    }

    void BuildTopics(RectTransform panel)
    {
        var area=LandscapeUI.Rect("TopicChoices",panel,24,210,1512,76);
        area.anchorMin=new Vector2(0,1);area.anchorMax=new Vector2(1,1);
        area.sizeDelta=new Vector2(-48,76);
        topics=area.gameObject.AddComponent<GridLayoutGroup>();
        topics.constraint=GridLayoutGroup.Constraint.FixedRowCount;topics.constraintCount=1;
        topics.spacing=new Vector2(10,0);topics.childAlignment=TextAnchor.MiddleCenter;
        AddTopic(area,"WEATHER","How is the weather today?");
        AddTopic(area,"MATCHDAY","What is happening on matchday?");
        AddTopic(area,"STADIUM","What is going on around the stadium?");
        AddTopic(area,"PUBS","What is happening at the local pub?");
        AddTopic(area,"EVENTS","Are there any upcoming events?");
        AddTopic(area,"JOIN OUR CREW","Would you like to join our crew?",true);
    }

    void AddTopic(RectTransform parent,string label,string question,bool invite=false)
    {
        var button=LandscapeUI.Button("Topic_"+label,parent,label,0,0,230,68,invite?"green":"dark");
        button.onClick.AddListener(()=>{if(invite)Invite();else AskQuestion(question);});
    }

    void Update()
    {
        if(!frame)return;
        if(topics)
        {
            float width=Mathf.Max(1600,frame.rect.width);
            float usable=width-64;
            topics.cellSize=new Vector2((usable-topics.spacing.x*5)/6f,68);
            if(questionBox)questionBox.sizeDelta=new Vector2(width-664,52);
            if(sendButton)sendButton.anchoredPosition=new Vector2(width-626,-144);
            if(voiceButton)voiceButton.anchoredPosition=new Vector2(width-442,-144);
            if(closeButton)closeButton.anchoredPosition=new Vector2(width-234,-144);
            if(transcriptRect)transcriptRect.sizeDelta=new Vector2(width-680,82);
            if(voiceStateRect)voiceStateRect.anchoredPosition=new Vector2(width-624,-18);
        }
        if(IsConversationOpen)
        {
            if(!current)Close();
            return;
        }
        if(Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+.2f;
        FindNearbyNpc();
    }

    void FindNearbyNpc()
    {
        nearby=null;speaker=null;
        if(Time.timeScale==0 || GamePopup.AnyOpen || BattleManager.instance==null)
        {if(nearbyRoot)nearbyRoot.SetActive(false);return;}
        float best=InteractionRange*InteractionRange;
        var agents=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive).ToArray();
        foreach(var npc in FindObjectsByType<SocialNpc>(FindObjectsSortMode.None))
        {
            if(!npc || npc.JoinedCrew)continue;
            foreach(var agent in agents)
            {
                Vector3 delta=npc.transform.position-agent.transform.position;delta.y=0;
                float distance=delta.sqrMagnitude;
                if(distance>=best)continue;
                best=distance;nearby=npc;speaker=agent;
            }
        }
        if(nearbyRoot)nearbyRoot.SetActive(false);
        if(nearbyLabel && nearby)nearbyLabel.text="TALK TO "+nearby.DisplayName;
    }

    public bool IsInRange(SocialNpc npc)
    {
        if(!npc || BattleManager.instance==null)return false;
        return BattleManager.instance.PlayerAgents.Any(a=>a&&a.IsAlive&&HorizontalDistance(a.transform.position,npc.transform.position)<=InteractionRange);
    }

    public bool TryOpen(SocialNpc npc)
    {
        if(!npc)return false;
        FindNearbyNpc();
        if(!IsInRange(npc))return false;
        current=npc;
        speaker=BattleManager.instance.PlayerAgents.Where(a=>a&&a.IsAlive)
            .OrderBy(a=>HorizontalDistance(a.transform.position,npc.transform.position)).FirstOrDefault();
        current.PauseForConversation(true,speaker? speaker.transform.position:current.transform.position+Vector3.forward);
        title.text=current.DisplayName+"  /  "+current.Venue;
        transcript.text=current.DisplayName+": "+current.Greeting;
        questionInput.text=string.Empty;
        voiceState.text="TYPE, CHOOSE A TOPIC, OR USE VOICE";
        nearbyRoot.SetActive(false);conversationRoot.SetActive(true);
        conversationRoot.transform.SetAsLastSibling();
        CityGameplay.Instance?.PostEvent("TALKING TO "+current.DisplayName);
        return true;
    }

    void AskQuestion(string question)
    {
        if(!current || string.IsNullOrWhiteSpace(question))return;
        transcript.text="YOU: "+question.Trim()+"\n"+current.DisplayName+": "+current.ReplyTo(question);
        questionInput.text=string.Empty;
    }

    void Invite()
    {
        if(!current)return;
        bool accepted=current.TryInvite(UnityEngine.Random.value,out string response);
        transcript.text="YOU: Would you like to join our crew?\n"+current.DisplayName+": "+response;
        voiceState.text=accepted?"INVITATION ACCEPTED (25% CHANCE)":"INVITATION DECLINED (75% CHANCE)";
    }

    public void Close()
    {
        StopVoiceInput();
        if(current)
        {
            var finished=current;
            current.PauseForConversation(false,speaker? speaker.transform.position:current.transform.position);
            current=null;
            finished.FinishJoinedConversation();
        }
        speaker=null;
        if(conversationRoot)conversationRoot.SetActive(false);
        FindNearbyNpc();
    }

    void StartVoiceInput()
    {
        if(!current)return;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using(var bridge=new AndroidJavaClass("com.hooliganmanager.voice.VoiceRecognitionActivity"))
            using(var unityPlayer=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                bridge.CallStatic("start",activity);
            voiceState.text="LISTENING...";
        }
        catch(Exception e){voiceState.text="VOICE SERVICE UNAVAILABLE - TYPE OR CHOOSE A TOPIC";Debug.LogWarning(e.Message);}
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            StopVoiceInput();
            dictation=new DictationRecognizer();
            dictation.DictationResult+=(text,confidence)=>OnVoiceResult(text);
            dictation.DictationError+=(error,hresult)=>{voiceState.text="VOICE ERROR - TYPE OR CHOOSE A TOPIC";};
            dictation.Start();voiceState.text="LISTENING... SPEAK YOUR QUESTION";
        }
        catch(Exception e){voiceState.text="VOICE SERVICE UNAVAILABLE - TYPE OR CHOOSE A TOPIC";Debug.LogWarning(e.Message);}
#else
        voiceState.text="VOICE IS NOT AVAILABLE ON THIS DEVICE - TYPE OR CHOOSE A TOPIC";
#endif
    }

    public void OnVoiceResult(string text)
    {
        if(string.IsNullOrWhiteSpace(text)||text=="__CANCELLED__")
        {voiceState.text="NO SPEECH HEARD - TRY AGAIN OR TYPE";return;}
        questionInput.text=text;voiceState.text="VOICE QUESTION RECEIVED";AskQuestion(text);StopVoiceInput();
    }

    void StopVoiceInput()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if(dictation!=null)
        {
            if(dictation.Status==SpeechSystemStatus.Running)dictation.Stop();
            dictation.Dispose();dictation=null;
        }
#endif
    }

    static float HorizontalDistance(Vector3 a,Vector3 b)
    {a.y=0;b.y=0;return Vector3.Distance(a,b);}

    void OnDestroy()
    {
        StopVoiceInput();
        if(Instance==this)Instance=null;
    }
}
