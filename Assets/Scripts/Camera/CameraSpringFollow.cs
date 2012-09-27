using UnityEngine;

[AddComponentMenu("Third Person Camera/Spring Follow Camera")]
public class CameraSpringFollow : MonoBehaviour
{
    public Transform Target;
    public float Distance = 4.0f;
    public float Height = 4.0f;
    public float SmoothLag = 0.2f;
    public float MaxSpeed = 10.0f;
    public float SnapLag = 0.3f;
    public float ClampHeadPositionScreenSpace = 0.75f;
    public LayerMask LineOfSightMask = 0;


    private bool _isSnapping;
    private Vector3 _headOffset = Vector3.zero;
    private Vector3 _centerOffset = Vector3.zero;
    private ThirdPersonController _controller;
    private Vector3 _velocity = Vector3.zero;
    private float _targetHeight = 100000.0f;
    

    private void Awake()
    {
        var characterController = Target.GetComponent<CharacterController>();
        if (characterController != null)
        {
            _centerOffset = characterController.bounds.center - Target.position;
            _headOffset = _centerOffset;
            _headOffset.y = characterController.bounds.max.y - Target.position.y;
        }

        if (Target != null)
        {
            _controller = Target.GetComponent<ThirdPersonController>();
        }

        if (_controller == null)
            Debug.Log("Please assign a target to the camera that has a Third Person Controller script component.");
    }

    private void LateUpdate()
    {
        var targetCenter = Target.position + _centerOffset;
        var targetHead = Target.position + _headOffset;

        // When jumping don't move the camera upwards but only down!
        if (_controller.IsJumping())
        {
            // We'd be moving the camera upwards, do that only if it's really high!
            float newTargetHeight = targetCenter.y + Height;
            if (newTargetHeight < _targetHeight || newTargetHeight - _targetHeight > 5)
                _targetHeight = targetCenter.y + Height;
        }
            // When walking always update the height
        else
        {
            _targetHeight = targetCenter.y + Height;
        }

        // We start snapping when user pressed Fire2!
        _isSnapping = Input.GetButton("Fire2");

        if (_isSnapping)
        {
            //ApplySnapping(targetCenter);
            ApplyPositionDamping(new Vector3(targetCenter.x, _targetHeight, targetCenter.z));

            //TODO: Optimize!!
            Transform t = GameObject.Find("Player").GetComponent<ThirdPersonTargetting>().GetCurrentTarget();

            //Set our target as the snapped object
            targetCenter = t.position;
        }
        else
        {
            ApplyPositionDamping(new Vector3(targetCenter.x, _targetHeight, targetCenter.z));
        }

        SetupRotation(targetCenter, targetHead);
    }

    private void ApplySnapping(Vector3 targetCenter)
    {
        var position = transform.position;
        var offset = position - targetCenter;
        offset.y = 0;
        var currentDistance = offset.magnitude;

        var targetAngle = Target.eulerAngles.y;
        var currentAngle = transform.eulerAngles.y;

        currentAngle = Mathf.SmoothDampAngle(currentAngle, targetAngle, ref _velocity.x, SnapLag);
        currentDistance = Mathf.SmoothDamp(currentDistance, Distance, ref _velocity.z, SnapLag);

        var newPosition = targetCenter;
        newPosition += Quaternion.Euler(0, currentAngle, 0) * Vector3.back * currentDistance;
        newPosition.y = Mathf.SmoothDamp(position.y, targetCenter.y + Height, ref _velocity.y, SmoothLag, MaxSpeed);

        newPosition = AdjustLineOfSight(newPosition, targetCenter);

        transform.position = newPosition;

        //We are close to the target, so we can stop snapping now!
        if (AngleDistance(currentAngle, targetAngle) < 3.0)
        {
            _isSnapping = false;
            _velocity = Vector3.zero;
        }
    }

    private Vector3 AdjustLineOfSight(Vector3 newPosition, Vector3 target)
    {
        RaycastHit hit;
        if (Physics.Linecast(target, newPosition, out hit, LineOfSightMask.value))
        {
            _velocity = Vector3.zero;
            return hit.point;
        }

        return newPosition;
    }

    private void ApplyPositionDamping(Vector3 targetCenter)
    {
        // We try to maintain a constant distance on the x-z plane with a spring.
        // Y position is handle with a seperate spring
        var position = transform.position;
        var offset = position - targetCenter;
        offset.y = 0;
        var newTargetPos = offset.normalized*Distance + targetCenter;

        var newPosition = Vector3.zero;
        if (_isSnapping)
        {
            //We are snapping so let's focus on the target while keeping the player in view
            newPosition.x = targetCenter.x;
            newPosition.z = targetCenter.z;
            newPosition.y = targetCenter.y;

            newPosition += transform.forward*-1f;
        }
        else
        {
            newPosition.x = Mathf.SmoothDamp(position.x, newTargetPos.x, ref _velocity.x, SmoothLag, MaxSpeed);
            newPosition.z = Mathf.SmoothDamp(position.z, newTargetPos.z, ref _velocity.z, SmoothLag, MaxSpeed);
            newPosition.y = Mathf.SmoothDamp(position.y, targetCenter.y, ref _velocity.y, SmoothLag, MaxSpeed);
        }

        newPosition = AdjustLineOfSight(newPosition, targetCenter);

        transform.position = newPosition;
    }

    private void SetupRotation(Vector3 centerPos, Vector3 headPos)
    {
        // Now it's getting hairy. The devil is in the details here, the big issue is jumping of course.
        // * When jumping up and down don't center the guy in screen space. This is important to give a feel for how high you jump.
        //   When keeping him centered, it is hard to see the jump.
        // * At the same time we dont want him to ever go out of screen and we want all rotations to be totally smooth
        //
        // So here is what we will do:
        //
        // 1. We first find the rotation around the y axis. Thus he is always centered on the y-axis
        // 2. When grounded we make him be centered
        // 3. When jumping we keep the camera rotation but rotate the camera to get him back into view if his head is above some threshold
        // 4. When landing we must smoothly interpolate towards centering him on screen

        Vector3 cameraPos = transform.position;
        Vector3 offsetToCenter = centerPos - cameraPos;

        // Generate base rotation only around y-axis
        Quaternion yRotation = Quaternion.LookRotation(new Vector3(offsetToCenter.x, 0, offsetToCenter.z));

        //Translate the rotation with a little dampening
        Vector3 relativeOffset = Vector3.forward*Distance + Vector3.down*Height;
        var transformRot = yRotation*Quaternion.LookRotation(relativeOffset);
        transform.rotation = Quaternion.Slerp(transform.rotation, transformRot, Time.deltaTime * 2.0f);

        // Calculate the projected center position and top position in world space
        Ray centerRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 1));
        Ray topRay = camera.ViewportPointToRay(new Vector3(0.5f, ClampHeadPositionScreenSpace, 1));

        Vector3 centerRayPos = centerRay.GetPoint(Distance);
        Vector3 topRayPos = topRay.GetPoint(Distance);

        float centerToTopAngle = Vector3.Angle(centerRay.direction, topRay.direction);

        float heightToAngle = centerToTopAngle/(centerRayPos.y - topRayPos.y);

        float extraLookAngle = heightToAngle*(centerRayPos.y - centerPos.y);
        if (extraLookAngle < centerToTopAngle)
        {
            extraLookAngle = 0;
        }
        else
        {
            extraLookAngle = extraLookAngle - centerToTopAngle;
            transform.rotation *= Quaternion.Euler(-extraLookAngle, 0, 0);
        }
    }

    private float AngleDistance(float a, float b)
    {
        a = Mathf.Repeat(a, 360);
        b = Mathf.Repeat(b, 360);
        return Mathf.Abs(b - a);
    }
}