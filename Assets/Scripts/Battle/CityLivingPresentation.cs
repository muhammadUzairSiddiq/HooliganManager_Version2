using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small reusable effects at actual damaged shops and verified water surfaces.</summary>
public sealed class CityLivingPresentation:MonoBehaviour
{
    sealed class DamageEffect{public GameObject Root;public ParticleSystem Smoke,Flame;public float Until;}
    public const int MaximumDamageEffects=2;
    public static CityLivingPresentation Instance{get;private set;}
    readonly List<DamageEffect> damage=new List<DamageEffect>();
    readonly List<MeshRenderer> water=new List<MeshRenderer>();
    ParticleSystem foam;
    Material smokeMaterial,flameMaterial,waterMaterial;
    float nextWater;
    public int ActiveDamageEffects=>damage.Count(e=>e.Root&&e.Root.activeSelf);
    public int VerifiedWaterSurfaces=>water.Count(r=>r);
    public bool WaterEffectActive=>foam&&foam.gameObject.activeSelf;
    public static CityLivingPresentation Ensure(GameObject owner)
    {return Instance?Instance:owner.GetComponent<CityLivingPresentation>()??owner.AddComponent<CityLivingPresentation>();}
    void Awake(){Instance=this;}
    void Start()
    {
        foreach(var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            string name=renderer.name.ToLowerInvariant();var bounds=renderer.bounds;
            if(name.Contains("water")&&!renderer.GetComponentInParent<Canvas>()&&bounds.size.y<1&&bounds.size.x>3&&bounds.size.z>3)water.Add(renderer);
        }
    }
    public void RaidDamage(Vector3 point)
    {
        if(GameFeedback.ReducedMotion)return;
        var effect=damage.FirstOrDefault(e=>!e.Root.activeSelf);
        if(effect==null&&damage.Count<MaximumDamageEffects){effect=CreateDamage();damage.Add(effect);}
        if(effect==null)effect=damage.OrderBy(e=>e.Until).First();
        effect.Root.transform.position=point+Vector3.up*.15f;effect.Until=Time.time+6;effect.Root.SetActive(true);
        effect.Smoke.Clear();effect.Flame.Clear();effect.Smoke.Play();effect.Flame.Play();
    }
    DamageEffect CreateDamage()
    {
        var root=new GameObject("Pooled damaged shop effect");root.transform.SetParent(transform);root.SetActive(false);
        if(!smokeMaterial)smokeMaterial=ParticleMaterial("City raid smoke",new Color(.23f,.22f,.2f,.3f));
        if(!flameMaterial)flameMaterial=ParticleMaterial("City raid sparks",new Color(1,.38f,.06f,.7f));
        return new DamageEffect{Root=root,Smoke=Emitter(root.transform,"Small shop smoke",smokeMaterial,20,.65f,.55f,.35f,10),Flame=Emitter(root.transform,"Small shop sparks",flameMaterial,14,.25f,.3f,.2f,12)};
    }
    static Material ParticleMaterial(string name,Color color)
    {
        var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Sprites/Default");var material=new Material(shader){name=name,color=color};
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
        if(material.HasProperty("_Surface"))material.SetFloat("_Surface",1);
        if(material.HasProperty("_SrcBlend"))material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
        if(material.HasProperty("_DstBlend"))material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        if(material.HasProperty("_ZWrite"))material.SetFloat("_ZWrite",0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;return material;
    }
    static ParticleSystem Emitter(Transform owner,string name,Material material,int maximum,float life,float speed,float size,float rate)
    {
        var go=new GameObject(name);go.transform.SetParent(owner,false);var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=system.main;main.loop=true;main.playOnAwake=false;main.duration=1;main.startLifetime=life;main.startSpeed=speed;main.startSize=size;main.maxParticles=maximum;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=system.emission;emission.rateOverTime=rate;var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=.7f;shape.angle=14;shape.rotation=new Vector3(-90,0,0);
        var fade=system.colorOverLifetime;fade.enabled=true;fade.color=new ParticleSystem.MinMaxGradient(new Gradient{colorKeys=new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},alphaKeys=new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.15f),new GradientAlphaKey(0,1)}});
        var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return system;
    }
    void Update()
    {
        if(GameFeedback.ReducedMotion){foreach(var effect in damage)if(effect.Root.activeSelf)effect.Root.SetActive(false);if(foam)foam.gameObject.SetActive(false);return;}
        foreach(var effect in damage)if(effect.Root.activeSelf&&Time.time>=effect.Until){effect.Smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);effect.Flame.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);effect.Root.SetActive(false);}
        if(Time.unscaledTime<nextWater)return;nextWater=Time.unscaledTime+1;
        var surface=water.Where(r=>r&&r.enabled&&r.gameObject.activeInHierarchy&&CityActivityStreaming.Interested(r.bounds.center)).OrderBy(r=>(r.bounds.center-CityActivityStreaming.GroundFocus(Camera.main)).sqrMagnitude).FirstOrDefault();
        if(!surface){if(foam)foam.gameObject.SetActive(false);return;}
        if(!foam)
        {
            waterMaterial=ParticleMaterial("Existing water light foam",new Color(.72f,.92f,1,.4f));foam=Emitter(transform,"Existing water surface foam",waterMaterial,12,.9f,.08f,.18f,4);
            var shape=foam.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(3,.02f,3);
        }
        var bounds=surface.bounds;foam.transform.position=new Vector3(bounds.center.x,bounds.max.y+.035f,bounds.center.z);foam.gameObject.SetActive(true);if(!foam.isPlaying)foam.Play();
    }
    void OnDisable(){foreach(var effect in damage)if(effect.Root)effect.Root.SetActive(false);if(foam)foam.gameObject.SetActive(false);}
    void OnDestroy(){if(Instance==this)Instance=null;if(smokeMaterial)Destroy(smokeMaterial);if(flameMaterial)Destroy(flameMaterial);if(waterMaterial)Destroy(waterMaterial);}
}
