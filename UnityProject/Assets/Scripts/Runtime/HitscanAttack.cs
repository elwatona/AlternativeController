using AC;
using Nebula;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EntityStates
{
    public class HitscanAttack
    {
        public GameObject attacker;
        public int hitscanCount;
        public Vector2 origin;
        public Vector2 direction;
        public float hitscanRadius;
        public float distance;
        public LayerMask hitMask;
        public LayerMask stopperMask;
        public float minAngleSpread;
        public float maxAngleSpread;
        public float baseDamage;
        public Xoroshiro128Plus rng;

        public FalloffCalculateDelegate falloffCalculation = DefaultFalloffCalculation;
        public HitCallback hitCallback = DefaultHitCallback;

        private RaycastHit2D[] _cachedHits;
        public void Fire()
        {
            rng ??= new Xoroshiro128Plus(ACApplication.instance.applicationRNG.nextULong);
            Vector2[] spreadArray = new Vector2[hitscanCount];
            for(int i = 0; i < hitscanCount; i++)
            {
                float angle = rng.RangeFloat(minAngleSpread, maxAngleSpread);
                Quaternion spreadRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                spreadArray[i] = spreadRotation * direction;
            };

            for(int i = 0; i < hitscanCount; i++)
            {
                FireSingle(spreadArray[i]);
            }
        }

        private void FireSingle(Vector2 normal)
        {
            Vector2 endPos = origin + normal * distance;
            List<Hit> bulletHit = new List<Hit>();
            if(hitscanRadius == 0)
            {
                _cachedHits = Physics2D.RaycastAll(origin, normal, distance, hitMask);
            }
            else
            {
                _cachedHits = Physics2D.CircleCastAll(origin, hitscanRadius, direction, distance, hitMask);
            }
            for(int i = 0; i < _cachedHits.Length; i++)
            {
                Hit hit = default;
                InitBulletHitFromRaycastHit(ref hit, origin, normal, ref _cachedHits[i]);
            
                if(hitCallback(this, ref hit))
                {
                    endPos = hit.hitPoint;
                    break;
                }
            }

#if DEBUG && UNITY_EDITOR
            GlobalGizmos.EnqueueGizmoDrawing(() =>
            {
                UnityEditor.Handles.color = Color.red;
                UnityEditor.Handles.DrawLine(origin, endPos, 5);
            });
#endif
        }

        private void InitBulletHitFromRaycastHit(ref Hit hit, Vector2 origin, Vector2 normal, ref RaycastHit2D raycastHit2D)
        {
            hit.hitColldier = raycastHit2D.collider;
            hit.hurtBox = raycastHit2D.collider.GetComponent<HurtBox>();
            hit.direction = normal;
            hit.distance = raycastHit2D.distance;
            hit.surfaceNormal = raycastHit2D.normal;
            hit.hitPoint = raycastHit2D.distance == 0 ? origin : raycastHit2D.point;
            hit.entityObject = (hit.hurtBox && hit.hurtBox.healthComponent) ? hit.hurtBox.healthComponent.gameObject : raycastHit2D.collider.gameObject;
        }

        public static float DefaultFalloffCalculation(float distance)
        {
            return 1;
        }

        public static float BuckshotFalloffCalculation(float distance)
        {
            return 0.25f + Mathf.Clamp01(Mathf.InverseLerp(25f, 7f, distance)) * 0.75f;
        }

        public static float BulletFalloffCalculation(float distance)
        {
            return 0.5f + Mathf.Clamp01(Mathf.InverseLerp(60f, 25f, distance)) * 0.5f;
        }

        public static bool DefaultHitCallback(HitscanAttack attack, ref Hit hit)
        {
            return false;
        }

        public delegate float FalloffCalculateDelegate(float distance);
        public delegate bool HitCallback(HitscanAttack attack, ref Hit hit);
    
        public struct Hit
        {
            public Vector2 direction;
            public Vector2 hitPoint;
            public Vector2 surfaceNormal;
            public float distance;
            public Collider2D hitColldier;
            public GameObject entityObject;
            public HurtBox hurtBox;
        }
    }
}