﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class Damage {

    public static void MeleeDamage(Vector3 origin, Vector3 direction, float damage, float range,float coneOffset, float angle, Entity damagingEntity,int enemiesToHit)
    {
        Vector3 offsetOrigin = origin - direction.normalized * coneOffset;
        var closestEntitiesWithinAngle = from entity in EntityRegistry.GetInstance().GetClosestEntities(offsetOrigin, range + coneOffset, damagingEntity)
                                         where Utility.WithinAngle(offsetOrigin, direction, entity.Position, angle) && Vector3.Distance(entity.Position, offsetOrigin) >= coneOffset && 
                                         Utility.IsVisible(origin,entity.gameObject,range,entity.verticalTargetingOffset)
                                         select new { entity, distance = Vector3.Distance(entity.Position, origin) };

        IEnumerable<Entity> entitiesToDamage = from entityInfo in closestEntitiesWithinAngle
                               //where entityInfo.distance == closestEntitiesWithinAngle.Min(e => e.distance)
                               orderby entityInfo.distance
                               select entityInfo.entity;

        // if entity's Position didn't get into the cone
        Ray forwardRay = new Ray(origin, direction);
        RaycastHit rayHit;
        if (Physics.Raycast(forwardRay, out rayHit, range))
        {
            Entity hitEntity = rayHit.collider.GetComponent<Entity>();
            if (hitEntity && !entitiesToDamage.Contains(hitEntity))
            {
                SendDamageFeedback(damagingEntity, hitEntity, hitEntity.TakeDamage(damage, damagingEntity, damagingEntity.Position));
                --enemiesToHit;
            }
        }


        for (int i = 0; i < Mathf.Min(enemiesToHit, entitiesToDamage.Count()) ; ++i)
        {
            Entity entity = entitiesToDamage.ElementAt(i);
            SendDamageFeedback(damagingEntity, entity, entity.TakeDamage(damage, damagingEntity, damagingEntity.Position));
        }
    }


    public static void ExplosiveDamage(Vector3 origin, float damage, float range, Entity damagingEntity)//float explosionForce
    {
        var closestVisibleEntities = from entity in EntityRegistry.GetInstance().GetClosestEntities(origin, range)
                                     where Utility.IsVisible(origin, entity.gameObject, range,entity.verticalTargetingOffset)
                                     select entity;

        foreach (Entity e in closestVisibleEntities)
        {
            float distance = Vector3.Distance(e.Position, origin);
            float distanceMultiplier = (distance / range) > 0.5f ? (distance / range) : 1;

            SendDamageFeedback(damagingEntity, e, e.TakeDamage(damage * distanceMultiplier, damagingEntity, origin));
            //if (e as Character)
            //    (e as Character).AddExplosionForce(origin, explosionForce * distanceMultiplier);
        }

        //PlayerCamera.ScreenShake(0.2f, origin);
    }

    public static void HitscanDamage(Vector3 origin, Vector3 direction, float damage, float range, Entity damagingEntity)
    {
        Ray ray = new Ray(origin, direction);
        RaycastHit hitInfo;
        if (Physics.Raycast(ray, out hitInfo, range))
        {
            Entity entity = hitInfo.collider.GetComponent<Entity>();
            if (entity)
                SendDamageFeedback(damagingEntity, entity, entity.TakeDamage(damage, damagingEntity, damagingEntity.Position));
        }
    }

    public static void ForcePush(Vector3 origin, Vector3 direction, float enemyRange,float deadlyObjectRange, float angle, Entity damagingEntity, int enemiesToHit,float stunDuration,float pushDuration,float pushSpeed,int enemyLayer,GameObject effectPrefab)
    {
        var closestAIWithinAngle = from entity in EntityRegistry.GetInstance().GetClosestEntities(origin, enemyRange, damagingEntity)
                                         where Utility.WithinAngle(origin, direction, entity.Position, angle) && Vector3.Distance(entity.Position, origin) <= enemyRange
                                            && Utility.IsVisible(origin, entity.gameObject, enemyRange, entity.verticalTargetingOffset)
                                            && entity as AICharacter
                                         select new { character = entity as AICharacter, distance = Vector3.Distance(entity.Position, origin) };

        IEnumerable<AICharacter> AIToPush = from charInfo in closestAIWithinAngle
                                               orderby charInfo.distance
                                               select charInfo.character;

        int layerMask = 1 << enemyLayer;
        layerMask = ~layerMask;

        var closestDeadlyObjectsWithinAngle = from deadlyObj in DeadlyObjectRegistry.GetInstance().GetClosestObjects(origin, deadlyObjectRange)
                                   where Utility.WithinAngle(origin, direction, deadlyObj.Position, angle) && Vector3.Distance(deadlyObj.Position, origin) <= deadlyObjectRange
                                      && Utility.IsVisible(origin, deadlyObj.gameObject, deadlyObjectRange, deadlyObj.verticalTargetingOffset, layerMask)
                                   select new { deadlyObj, distance = Vector3.Distance(deadlyObj.Position, origin) };
        
        IEnumerable<DeadlyObject> ClosestDeadlyObjects = from objInfo in closestDeadlyObjectsWithinAngle
                                                       orderby objInfo.distance
                                                       select objInfo.deadlyObj;
        DeadlyObject closestObject = ClosestDeadlyObjects.Count() > 0? ClosestDeadlyObjects.First() : null;

        for (int i = 0; i < Mathf.Min(enemiesToHit, AIToPush.Count()); ++i)
        {
            AICharacter character = AIToPush.ElementAt(i);
            float duration = character.aiStats.pushDurationMultiplier * pushDuration;
            
            character.Stun(stunDuration, damagingEntity);
            Vector3 pushDirection = (closestObject && character.aiStats.canBeStunned) ? closestObject.Position - character.Position : character.Position - origin;

            if (effectPrefab)
                GameObject.Instantiate(effectPrefab, character.Position, Quaternion.LookRotation(pushDirection), character.transform).AddComponent<DestroyOnTime>().delay = duration;
            character.StartCoroutine(ForcePushRoutine(duration, pushSpeed, character, pushDirection));
        }

    }

    private static IEnumerator ForcePushRoutine(float pushDuration,float pushSpeed,AICharacter aICharacter,Vector3 direction)
    {
        direction.Normalize();
        float timeCounter = pushDuration;
        while(timeCounter > 0)
        {
            aICharacter.JustMove(direction * pushSpeed * Time.deltaTime);
            yield return null;
            timeCounter -= Time.deltaTime;
        }
    }

    public static void SendDamageFeedback(Entity entity,Entity damagedEntity,Entity.DamageOutcome damageOutcome)
    {
        if (entity && entity as Character)
            (entity as Character).DamageFeedback(damagedEntity, damageOutcome);
    }
}
