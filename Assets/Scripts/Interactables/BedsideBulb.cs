using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class BedsideBulb : MonoBehaviour
    {
        private GameObject _bulb;
        private AudioSource _shatterAudio;
        private ParticleSystem _shards;
        private Mesh _shardMesh;
        private Material _glassMaterial;
        private bool _broken;

        public void Initialize(Light light)
        {
            _glassMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            _glassMaterial.color = new Color(0.75f, 0.9f, 1f);
            _bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _bulb.name = "BedsideBulbGlass";
            // Imported furniture uses a scaled parent; set world size before parenting.
            _bulb.transform.position = light.transform.position;
            _bulb.transform.localScale = new Vector3(0.065f, 0.09f, 0.065f);
            _bulb.transform.SetParent(transform, true);
            Destroy(_bulb.GetComponent<Collider>());
            _bulb.GetComponent<Renderer>().sharedMaterial = _glassMaterial;

            _shatterAudio = gameObject.AddComponent<AudioSource>();
            AudioRouting.Assign(_shatterAudio, AudioBus.Horror);
            _shatterAudio.playOnAwake = false;
            _shatterAudio.spatialBlend = 1f;
            _shatterAudio.minDistance = 2f;
            _shatterAudio.maxDistance = 12f;
            _shatterAudio.volume = 0.7f;
            _shatterAudio.clip = Resources.Load<AudioClip>("Audio/BulbShatter");

            GameObject effect = new GameObject("BulbGlassShards");
            effect.transform.position = light.transform.position;
            effect.transform.SetParent(transform, true);
            _shards = effect.AddComponent<ParticleSystem>();
            _shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _shards.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
            main.gravityModifier = 1f;
            main.maxParticles = 35;
            var emission = _shards.emission;
            emission.enabled = false;
            var shape = _shards.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.015f;
            var rotation = _shards.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
            var collision = _shards.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.bounce = 0.25f;
            collision.lifetimeLoss = 0.4f;
            _shardMesh = new Mesh { name = "BulbShard" };
            _shardMesh.vertices = new[] { new Vector3(-0.5f, -0.3f, 0), new Vector3(0.5f, -0.4f, 0), new Vector3(0.1f, 0.6f, 0) };
            _shardMesh.triangles = new[] { 0, 1, 2, 2, 1, 0 };
            _shardMesh.RecalculateNormals();
            var renderer = _shards.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = _shardMesh;
            renderer.sharedMaterial = _glassMaterial;
        }

        public void Break()
        {
            if (_broken || _bulb == null) return;
            _broken = true;
            _bulb.SetActive(false);
            _shards.Play();
            _shards.Emit(30);
            if (_shatterAudio.clip != null) _shatterAudio.Play();
        }

        private void OnDestroy()
        {
            if (_glassMaterial != null) Destroy(_glassMaterial);
            if (_shardMesh != null) Destroy(_shardMesh);
        }
    }
}
