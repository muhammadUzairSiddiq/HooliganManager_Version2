using UnityEngine;

[CreateAssetMenu(menuName="Hooligan/Role Character Models")]
public sealed class RoleCharacterModels : ScriptableObject
{
    public CharacterPortraitRegistry civilians, gangs, police;
    static RoleCharacterModels cached;
    public static RoleCharacterModels Current { get { if(!cached)cached=Resources.Load<RoleCharacterModels>("RoleCharacterModels");return cached;} }
}
