using System.Collections;
using UnityEngine;

public class ThirdPersonPlayerAnimation : MonoBehaviour
{
    public float RunSpeedScale = 1.0f;
    public float WalkSpeedScale = 1.0f;

    // Use this for initialization
    public void Start()
    {
        // By default loop all animations
        animation.wrapMode = WrapMode.Loop;

        if (animation["run"])
            animation["run"].layer = -1;

        if (animation["walk"])
            animation["walk"].layer = -1;

        if (animation["idle"])
            animation["idle"].layer = -2;

        animation.SyncLayer(-1);

        if (animation["ledgefall"] != null)
        {
            animation["ledgefall"].layer = 9;
            animation["ledgefall"].wrapMode = WrapMode.Loop;
        }


        // The jump animation is clamped and overrides all others
        if (animation["jump"] != null)
        {
            animation["jump"].layer = 10;
            animation["jump"].wrapMode = WrapMode.ClampForever;
        }

        if (animation["jumpfall"] != null)
        {
            animation["jumpfall"].layer = 10;
            animation["jumpfall"].wrapMode = WrapMode.ClampForever;
        }

        // This is the jet-pack controlled descent animation.
        if (animation["jetpackjump"])
        {
            animation["jetpackjump"].layer = 10;
            animation["jetpackjump"].wrapMode = WrapMode.ClampForever;
        }

        if (animation["jumpland"])
        {
            animation["jumpland"].layer = 10;
            animation["jumpland"].wrapMode = WrapMode.Once;
        }

        if (animation["walljump"])
        {
            animation["walljump"].layer = 11;
            animation["walljump"].wrapMode = WrapMode.Once;
        }

        // we actually use this as a "got hit" animation
        if (animation["buttstomp"])
        {
            animation["buttstomp"].speed = 0.15f;
            animation["buttstomp"].layer = 20;
            animation["buttstomp"].wrapMode = WrapMode.Once;
            AnimationState punch = animation["punch"];
            punch.wrapMode = WrapMode.Once;
        }

        // We are in full control here - don't let any other animations play when we start
        animation.Stop();
        animation.Play("idle");
    }

    // Update is called once per frame
    public void Update()
    {
        var playerController = GetComponent<ThirdPersonController>();
        float currentSpeed = playerController.GetSpeed();

        // Fade in run
        if (currentSpeed > playerController.WalkSpeed)
        {
            if (animation["run"])
                animation.CrossFade("run");

            // We fade out jumpland quick otherwise we get sliding feet
            if (animation["jumpland"])
                animation.Blend("jumpland", 0);
        }
            // Fade in walk
        else if (currentSpeed > 0.1)
        {
            if (animation["walk"])
                animation.CrossFade("walk");

            // We fade out jumpland realy quick otherwise we get sliding feet
            if (animation["jumpland"])
                animation.Blend("jumpland", 0);
        }
            // Fade out walk and run
        else
        {
            animation.Blend("walk", 0.0f, 0.3f);
            animation.Blend("run", 0.0f, 0.3f);
            animation.Blend("run", 0.0f, 0.3f);
        }

        animation["run"].normalizedSpeed = RunSpeedScale;
        animation["walk"].normalizedSpeed = WalkSpeedScale;

        if (playerController.IsJumping())
        {
            if (playerController.IsControlledDescent())
            {
                if (animation["jetpackjump"])
                    animation.CrossFade("jetpackjump", 0.2f);
            }
            else if (playerController.HasJumpReachedApex())
            {
                if (animation["jumpfall"])
                    animation.CrossFade("jumpfall", 0.2f);
            }
            else
            {
                if (animation["jump"])
                    animation.CrossFade("jump", 0.2f);
            }
        }
            // We fell down somewhere
        else if (!playerController.IsGroundedWithTimeout())
        {
            if (animation["ledgefall"] != null)
                animation.CrossFade("ledgefall", 0.2f);
        }
            // We are not falling down anymore
        else
        {
            if (animation["ledgefall"] != null)
                animation.Blend("ledgefall", 0.0f, 0.2f);
        }
    }

    public void DidLand()
    {
        if (animation["jumpland"])
            animation.Play("jumpland");
    }

    public void DidButtStomp()
    {
        animation.CrossFade("buttstomp", 0.1f);
        animation.CrossFadeQueued("jumpland", 0.2f);
    }

    public IEnumerator Slam()
    {
        animation.CrossFade("buttstomp", 0.2f);
        var playerController = GetComponent<ThirdPersonController>();
        while (!playerController.IsGrounded())
        {
            yield return null;
        }
        animation.Blend("buttstomp", 0, 0);
    }


    public void DidWallJump()
    {
        // Wall jump animation is played without fade.
        // We are turning the character controller 180 degrees around when doing a wall jump so the animation accounts for that.
        // But we really have to make sure that the animation is in full control so 
        // that we don't do weird blends between 180 degree apart rotations
        animation.Play("walljump");
    }
}