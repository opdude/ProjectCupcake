using System.Collections;
using UnityEngine;

/// <summary>
/// RespawnPoint: Allows the player to respan to this point in the level, effectively saving their progress
/// 
/// The RespawnPoint object has three main states and one interim state: Inactive, Active and Respawn, plus Triggered
/// 
/// - Inactive: Player hasn't reached this point and the player will not respawn here
/// - Active: Player has touched this respawn point and they will respawn here
/// - Respawn: Player is respawning at this point
/// 
/// Each state has it's own visual effect(s)
/// 
/// Respawn objects also require a simple collider, so the player can activat them.
/// </summary>

[RequireComponent(typeof(AudioSource))]
public class RespawnPoint : MonoBehaviour
{
    #region RespawnState enum

    public enum RespawnState
    {
        Inactive,
        Active,
        Respawn,
        Triggered
    }

    #endregion

    public RespawnPoint InitalRespawn; // Set this to the inital respawn point for the level

    public static RespawnPoint CurrentRespawn;
    public RespawnState CurrentState;

    //Sound Effects
    public AudioClip SFXPlayerActivate;
    public AudioClip SFXPlayerActiveLoop;
    public AudioClip SFXPlayerRespawn;
    public float SFXVolume = 1.0f; // Volume for one-shot sounds

    //Particle Emitters
    private ParticleSystem _emitterActive;
    private ParticleSystem _emitterInactive;
    private ParticleSystem _emitterRespawn1;
    private ParticleSystem _emitterRespawn2;
    private ParticleSystem _emitterRespawn3;

    //Light
    private Light _respawnLight;

    // Use this for initialization
    public void Start()
    {
        // Get some of the objects we need later
        _emitterActive = transform.Find("RSParticlesActive").GetComponent<ParticleSystem>();
        _emitterInactive = transform.Find("RSParticlesInactive").GetComponent<ParticleSystem>();
        _emitterRespawn1 = transform.Find("RSParticlesRespawn1").GetComponent<ParticleSystem>();
        _emitterRespawn2 = transform.Find("RSParticlesRespawn2").GetComponent<ParticleSystem>();
        _emitterRespawn3 = transform.Find("RSParticlesRespawn3").GetComponent<ParticleSystem>();

        _respawnLight = transform.Find("RSSpotlight").GetComponent<Light>();

        CurrentState = RespawnState.Inactive;

        // Set up the looping RespawnActive but leave it switched off for now
        if (SFXPlayerActiveLoop)
        {
            audio.clip = SFXPlayerActiveLoop;
            audio.loop = true;
            audio.playOnAwake = false;
        }

        // Assign the respawn point to be this one - Since the player is positioned on top of a respawn point,
        // it will come in and overwrite it, this is just to make sure there is always one active
        if (InitalRespawn != null)
            CurrentRespawn = InitalRespawn;
        if (CurrentRespawn == this)
            SetActive();
    }

    public void OnTriggerEnter()
    {
        if (CurrentRespawn != this) // Make sure we're not respawning or re-activing an already active point
        {
            // Turn the old respawn off
            if (CurrentRespawn)
                CurrentRespawn.SetInactive();
            else
                Debug.LogWarning("No intial respawn point set for this level.");

            // Play the "Activated" one-shot sound effect
            if (SFXPlayerActivate)
                AudioSource.PlayClipAtPoint(SFXPlayerActivate, transform.position, SFXVolume);

            // Set the current respawn point to this one
            CurrentRespawn = this;

            SetActive();
        }
    }

    public void SetActive()
    {
        _emitterActive.Play();
        _emitterInactive.Stop();
        _respawnLight.intensity = 1.5f;

        // Start the audio loop
        audio.Play();
    }

    public void SetInactive()
    {
        _emitterActive.Stop();
        _emitterInactive.Play();
        _respawnLight.intensity = 0;

        audio.Stop();
    }

    public IEnumerator FireEffect()
    {
        //Launch all 3 of the particle systems
        _emitterRespawn1.Play();
        _emitterRespawn2.Play();
        _emitterRespawn3.Play();

        _respawnLight.intensity = 3.5f;

        if (SFXPlayerRespawn)
            AudioSource.PlayClipAtPoint(SFXPlayerRespawn, transform.position, SFXVolume);

        yield return new WaitForSeconds(2);

        _respawnLight.intensity = 2.0f;
    }
}