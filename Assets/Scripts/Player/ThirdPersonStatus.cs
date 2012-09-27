using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the players state machine.
/// Keeps track of inventory, health, lives etc.
/// </summary>
public class ThirdPersonStatus : MonoBehaviour {

    public int Health = 6;
    public int MaxHealth = 6;
    public int Lives = 4;

    //Sound Effects
    public AudioClip struckSound;
    public AudioClip landingSound;
    public AudioClip deathSound;
    public AudioClip fallingSound;

    public void Awake()
    {
    }

    public void ApplyDamage(int damage)
    {
        if (struckSound != null)
            AudioSource.PlayClipAtPoint(struckSound, transform.position);

        Health -= damage;
        if (Health <= 0)
        {
            SendMessage("Die");
        }

        Mathf.Clamp(Health, 0, MaxHealth);
    }

    public void AddHealth(int health)
    {
        Health += health;
        if (Health > MaxHealth)
        {
            Lives++;
        }
        Mathf.Clamp(Health, 0, MaxHealth);
    }

    public void FalloutDeath()
    {
        Die();
    }

    public IEnumerator Die()
    {
        if (deathSound)
            AudioSource.PlayClipAtPoint(deathSound, transform.position);

        //Reset health and remove a life
        Lives--;
        Health = MaxHealth;

        if (Lives<0) 
            Application.LoadLevel("GameOver");

        // If we've reached here, the player still has lives remaining, so respawn.
        Vector3 respawnPos = RespawnPoint.CurrentRespawn.transform.position;
        Camera.main.transform.position = respawnPos - (transform.forward*4) + Vector3.up;

        //Hide the player briefly to give the death sound time to finish...
        SendMessage("HidePlayer");

        //Relocate the player so we can see the respawn point!
        transform.position = respawnPos + Vector3.up;

        yield return new WaitForSeconds(1.6f);
        SendMessage("ShowPlayer");
        RespawnPoint.CurrentRespawn.FireEffect();
    }
}
