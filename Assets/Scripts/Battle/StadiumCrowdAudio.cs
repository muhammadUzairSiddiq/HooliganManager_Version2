using UnityEngine;
public sealed class StadiumCrowdAudio : MonoBehaviour
{
    AudioSource ambience, cheer;
    float nextCheer;
    void Start()
    {
        ambience=Make("stadium_ambience",true,.32f);cheer=Make("stadium_cheer",false,.45f);
        nextCheer=Time.time+Random.Range(10f,24f);
    }
    AudioSource Make(string clip,bool loop,float volume)
    {
        var source=gameObject.AddComponent<AudioSource>();source.clip=Resources.Load<AudioClip>("Audio/"+clip);
        source.loop=loop;source.volume=volume*GameFeedback.SfxGain;source.mute=GameFeedback.SfxMuted;source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Linear;
        source.minDistance=65;source.maxDistance=260;source.dopplerLevel=0;source.playOnAwake=false;
        if(loop&&source.clip)source.Play();return source;
    }
    void Update()
    {
        if(ambience) { ambience.volume=.32f*GameFeedback.SfxGain; ambience.mute=GameFeedback.SfxMuted; }
        if(cheer) { cheer.volume=Mathf.Lerp(.2f,.5f,(GameManager.Data?.FanMorale??50)/100f)*GameFeedback.SfxGain; cheer.mute=GameFeedback.SfxMuted; }
        if(Time.time<nextCheer)return;
        nextCheer=Time.time+Random.Range(24f,45f);
        if(cheer&&cheer.clip&&!cheer.isPlaying&&GameFeedback.SfxGain>0)cheer.Play();
    }
}
