using UnityEngine;

[CreateAssetMenu(menuName = "Weapon/Animation Class", fileName = "WeaponAnimationClass")]
public sealed class WeaponAnimationClass : ScriptableObject
{
    [Header("Animation")]
    [Tooltip("Animator weapon-family parameter, for example Sword, Spear, Javelin, Slinger, Axe, or BasicBow.")]
    public string animationType;

    [Header("Held Weapon Pose")]
    public bool overrideVisualPose;
    public Vector2 visualOffset = new Vector2(.146f, -.082f);
    public float visualAngle;

    [Header("Projectile")]
    [Tooltip("Exact in-flight sprite. Blank falls back to the weapon's Throwable prefab.")]
    public Sprite projectileSprite;
    [Tooltip("Rotation needed to make the authored projectile sprite point right. Use -90 for art drawn pointing upward.")]
    public float projectileAngleOffset;
}
