using EntityStates;
using Nebula;
using Nebula.Serialization;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AC
{
    [RequireComponent(typeof(CharacterMaster))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EntityStateMachine))]
    public class BaseAI : MonoBehaviour, ICharacterInputProvider
    {
        public CharacterMaster characterMaster { get; private set; }
        public NavMeshAgent navMeshAgent { get; private set; }

        public Vector2 movementVector { get; private set; }

        public int rotationInput { get; private set; }

        public bool primaryInput { get; private set; }

        public bool secondaryInput { get; private set; }

        public bool specialInput { get; private set; }

        public Target currentTarget
        {
            get => _currentTarget;
            set
            {
                if(_currentTarget != value)
                {
                    _currentTarget = value;
                }
            }
        }
        public Target _currentTarget;
        public Xoroshiro128Plus aiRNG { get; private set; }
        public EntityStateMachine stateMachine { get; private set; }
        public ResourceDefPreference? resourceDefPreference { get; private set; }

        [Tooltip("How far this AI can see, this is used for LOS checks.")]
        public float visionRange;
        [Tooltip("If this value is false, the los check will return true as long as there's nothing between us and the enemy.")]
        public bool losCheckRequiresDirectContactWithTarget;
        [Tooltip("The maximum DOT product between a potential new target and the AI's body. This basically governs the angle the AI can look for targets and LOS checks. set to -1 to have a complete 360� vision range")]
        [Range(-1, 1)]
        public float maxDot;
#if DEBUG
        public GameObject testGameObject;
        [Nebula.DisabledField]
        public Vector3 currentTargetPosition;
        [Nebula.DisabledField]
        public GameObject currentTargetObject;
#endif

        private void Awake()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            characterMaster = GetComponent<CharacterMaster>();
            stateMachine = GetComponent<EntityStateMachine>();
            resourceDefPreference = GetComponent<ResourceDefPreference>();
        }

        // Start is called before the first frame update
        void Start()
        {
            navMeshAgent.updatePosition = false;
            navMeshAgent.updateRotation = false;
            navMeshAgent.updateUpAxis = false;
            aiRNG = new Xoroshiro128Plus(ACApplication.instance.applicationRNG.nextULong);
        }

        private void FixedUpdate()
        {
            if(!characterMaster.bodyInstance)
            {
                return;
            }
            navMeshAgent.nextPosition = characterMaster.bodyInstance.transform.position;
            navMeshAgent.speed = characterMaster.bodyInstance.movementSpeed;

            if (!navMeshAgent.hasPath)
                return;

            movementVector = navMeshAgent.desiredVelocity.normalized;
        }

#if DEBUG
        private void OnDrawGizmos()
        {
            Transform t = transform;

            if (Application.isPlaying && characterMaster.bodyInstance)
            {
                t = characterMaster.bodyInstance.transform;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(t.position, visionRange);

            var up = t.up;
            float angle = Mathf.Acos(maxDot);
            Vector3 axis = Vector3.Cross(up, Vector3.right);
            if (axis.magnitude < 0.01f)
            {
                axis = Vector3.Cross(up, Vector3.forward);
            }
            Quaternion rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis);
            Vector3 rotatedVector = rotation * up;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(t.position, t.position + rotatedVector * visionRange);

            rotation = Quaternion.AngleAxis(-angle * Mathf.Rad2Deg, axis);
            rotatedVector = rotation * up;
            Gizmos.DrawLine(t.position, t.position + rotatedVector * visionRange);
        }
#endif

        public class Target
        {
            public Vector3 targetPosition => targetIsObject ? targetTransform.position : _targetPosition;
            private Vector3 _targetPosition;
            public bool targetIsObject { get; }
            public GameObject targetObject { get;}
            public Transform targetTransform { get; }
            public bool hasMainHurtBox { get; }
            public HurtBox mainHurtBox { get; }

            public bool TestLineOfSight(BaseAI baseAI)
            {
                var charMaster = baseAI.characterMaster;
                if (!charMaster.bodyInstance)
                    return false;

                Transform bodyInstanceTransform = charMaster.bodyInstance.transform;
                Vector2 raycastDir = Vector2.zero;
                if(baseAI.maxDot > -1) //Check for dot before LOS
                {
                    var generalDirection = bodyInstanceTransform.up;
                    var directionForTarget = (hasMainHurtBox ? mainHurtBox.transform.position : targetPosition) - bodyInstanceTransform.position;
                    directionForTarget.Normalize();

                    var dotProduct = Vector3.Dot(generalDirection, directionForTarget);
                    if(dotProduct < baseAI.maxDot)
                    {
                        return false;
                    }
                    raycastDir = directionForTarget;
                }
                else
                {
                    raycastDir = Vector3.Normalize((hasMainHurtBox ? mainHurtBox.transform.position : targetPosition) - bodyInstanceTransform.position);
                }
                Ray ray = new Ray(bodyInstanceTransform.position, raycastDir);
                float distance = Mathf.Min(baseAI.visionRange, Vector3.Distance(bodyInstanceTransform.position, targetPosition));
                //Target is not an object OR if the target is an object but DOES NOT have a hurtbox., we should check if there's a wall not letting us look
                if(!targetIsObject || (targetIsObject && !mainHurtBox))
                {
                    if(Util.CharacterRaycast(bodyInstanceTransform.gameObject, ray, distance, LayerIndex.world.mask, out _))
                    {
                        //We've hit something on the world layer, we cant see it.
                        return false;
                    }
                    //We've hit nothing, path is clear
                    return true;
                }
                //We should raycast to see if we have direct LOS to our target's hurtbox.
                if(mainHurtBox && Util.CharacterRaycast(bodyInstanceTransform.gameObject, ray, distance, LayerIndex.CommonMasks.bullet, out var hit))
                {
                    if(!hit.collider.TryGetComponent<HurtBox>(out var hitHurtbox)) //We didnt hit a hurtbox
                    {
                        return false;
                    }

                    //Check if the hurtbox we've hit is either the main hurtbox, or if both hurtboxes point to the same health component.
                    return hitHurtbox == mainHurtBox || hitHurtbox.healthComponent == mainHurtBox.healthComponent;
                }
                return !baseAI.losCheckRequiresDirectContactWithTarget;
            }

            public Target(GameObject targetObject)
            {
                targetIsObject = targetObject;
                targetTransform = targetObject.transform;
                if (targetObject.TryGetComponent<HurtBoxGroup>(out var group))
                {
                    mainHurtBox = group.mainHurtBox;
                }
                else
                {
                    mainHurtBox = targetObject.GetComponent<HurtBox>();
                }
            }

            public Target(Vector3 position)
            {
                _targetPosition = position;
            }
        }
    }
}
