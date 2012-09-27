using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonTargetting : MonoBehaviour
{
    public Texture CurrentTargetTexture;
    public float MaxTargetSize = 100;
    public float MinTargetSize = 30;
    public float MaxCurrentTargetDistance = 20; // The distance in which we can use the target for lock-on
    public float MaxTargetDistance = 40; // The distance in which we can see the targets on the screen
    public Texture OtherTargetTexture; // The texture to be shown for all non current targets
    public Texture OffScreenTargetTexture;
    public Texture Dot;

    //TODO: Change to Enemy
    private Transform _currentTarget;
    private Transform _myTransform; // Cache of transform for optimization
    private readonly List<Transform> _targets = new List<Transform>();

    // Use this for initialization
    public void Start()
    {
        _myTransform = transform;

        //TODO: Move this so that enemies throw us a call that tells
        //us they are in range
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
            _targets.Add(enemy.transform);
    }

    // Update is called once per frame
    public void Update()
    {
        //TODO: Possibly should optimize this
        if (_targets.Count > 0)
        {
            SortTargets();
            _currentTarget = _targets[0];
        }
    }

    public void OnGUI()
    {
        foreach (Transform target in _targets)
        {
            float targetSize = (1/Vector3.Distance(_myTransform.position, target.transform.position))*1000;
            targetSize = Mathf.Clamp(targetSize, MinTargetSize, MaxTargetSize);

            Vector3 targetScreenPos = Camera.mainCamera.WorldToScreenPoint(target.position);
            targetScreenPos.y = Screen.height - targetScreenPos.y;

            Rect screenRect = new Rect(0,0,Screen.width, Screen.height);
            if (targetScreenPos.z < 0 || !screenRect.Contains(targetScreenPos))
            {
                var inverse = Camera.mainCamera.transform.InverseTransformPoint(target.position);
                inverse.Normalize();

                DrawOffScreenTarget(inverse);
            }
            else
            {
                if (target == _currentTarget)
                {
                    DrawTarget(targetScreenPos, targetSize, CurrentTargetTexture);
                }
                else
                {
                    DrawTarget(targetScreenPos, targetSize, OtherTargetTexture);
                }
            }
        }

        GUI.DrawTexture(new Rect((Screen.width /2) - (Dot.width / 2),(Screen.height /2) - (Dot.height / 2),Dot.width, Dot.height),Dot);
    }

    /// <summary>
    /// Draw a target on the GUI
    /// </summary>
    /// <param name="targetScreenPos">The target location in screen coords</param>
    /// <param name="targetSize">The size of the target</param>
    /// <param name="texture">The texture to draw for the target</param>
    private void DrawTarget(Vector3 targetScreenPos, float targetSize, Texture texture)
    {
        if (texture != null)
        {
            GUI.DrawTexture(
                new Rect((targetScreenPos.x - (targetSize/2)), (targetScreenPos.y - (targetSize/2)), targetSize,
                         targetSize), texture);
        }
    }

    private void DrawOffScreenTarget(Vector3 targetScreenPos)
    {
        Debug.Log(targetScreenPos);

        float test = (Screen.width/100.0f)*90.0f;

        targetScreenPos.x = (targetScreenPos.x * Screen.width) + (Screen.width / 2.0f);
        targetScreenPos.y = (-targetScreenPos.y * Screen.height) + (Screen.height / 2.0f);

        targetScreenPos.x = Mathf.Clamp(targetScreenPos.x, 0, Screen.width);
        targetScreenPos.y = Mathf.Clamp(targetScreenPos.y, 0, Screen.height);

        GUI.DrawTexture(new Rect(targetScreenPos.x - (OffScreenTargetTexture.width / 2.0f), 
            targetScreenPos.y - (OffScreenTargetTexture.height / 2.0f), OffScreenTargetTexture.width, OffScreenTargetTexture.height), OffScreenTargetTexture);
    }

/// <summary>
    /// Add a target to our players target list
    /// </summary>
    /// <param name="target"></param>
    public void AddTarget(Transform target)
    {
        _targets.Add(target);
        SortTargets();
    }

    /// <summary>
    /// Remove a target from our players target list, this happens
    /// when the player is out of range or the target dies/hides
    /// </summary>
    /// <param name="target"></param>
    public void RemoveTarget(Transform target)
    {
        _targets.Remove(target);
        SortTargets();
    }

    public Transform GetCurrentTarget()
    {
        return _currentTarget;
    }

    /// <summary>
    /// Sort our enemies by distance and visiblity to the player
    /// </summary>
    private void SortTargets()
    {
        _targets.Sort(delegate(Transform t1, Transform t2)
                          {
                              //TODO: Go furhter and cast a ray to make sure we can see the object, i.e not behind a wall
                              bool t1Visible = t1.renderer.IsVisibleFrom(Camera.mainCamera);
                              bool t2Visible = t2.renderer.IsVisibleFrom(Camera.mainCamera);

                              if (!t1Visible && t2Visible)
                              {
                                  return 1;
                              }
                              else if (t1Visible && !t2Visible)
                              {
                                  return -1;
                              }
                              else
                              {
                                  return
                                      Vector3.Distance(t1.position, _myTransform.position).CompareTo(
                                          Vector3.Distance(t2.position, _myTransform.position));
                              }
                          });
    }
}