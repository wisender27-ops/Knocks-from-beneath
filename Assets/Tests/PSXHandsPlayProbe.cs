#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KnocksFromBeneath;
using UnityEngine;
public sealed class PSXHandsPlayProbe : MonoBehaviour
{
    [Serializable] private sealed class Report { public string startedUtc,completedUtc,stage;public List<string> passed=new List<string>(),failed=new List<string>(); }
    private readonly Report report=new Report();
    private ProtagonistBody body;private PlayerController player;private PickupController pickup;private PlayerInventory inventory;private Camera view;private string output;private GameObject sample;
    private float mouseSensitivity; private PlayerInteraction interaction; private bool interactionEnabled;
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Begin()=>new GameObject("PSX hands verification").AddComponent<PSXHandsPlayProbe>();
    private IEnumerator Start()
    {
        output=Path.GetFullPath(".codex/verification/psx-hands");Directory.CreateDirectory(output);report.startedUtc=DateTime.UtcNow.ToString("O");
        var intro=FindFirstObjectByType<IntroSequence>();intro.CancelInvoke();intro.StopAllCoroutines();NarrativeDisplayQueue.InterruptAndEnqueue(this,Empty);
        body=FindFirstObjectByType<ProtagonistBody>();player=body.GetComponentInParent<PlayerController>();pickup=player.GetComponent<PickupController>();inventory=player.GetComponent<PlayerInventory>();view=player.GetComponentInChildren<Camera>();
        mouseSensitivity=player.mouseSensitivity;player.mouseSensitivity=0f;interaction=player.GetComponent<PlayerInteraction>();interactionEnabled=interaction.enabled;interaction.enabled=false;
        Teleport(new Vector3(-39.25f,.35f,124f));player.transform.rotation=Quaternion.Euler(0f,180f,0f);SetPitch(0f);yield return new WaitForSeconds(.7f);
        Check(body.Animator.avatar.isValid&&body.Animator.avatar.isHuman,"PSX avatar valid");Check(body.Animator.runtimeAnimatorController.animationClips.Distinct().Count()==14,"All fourteen previous animations retained");Check(!body.Animator.applyRootMotion,"Character animations do not drive player root");
        Stage("empty-hands");
        foreach(float pitch in new[]{0f,65f,-65f})
        {
            SetPitch(pitch);yield return new WaitForSeconds(.4f);CheckHands("empty pitch "+pitch);Capture("empty-"+pitch);
        }
        SetPitch(0f);
        var clips=body.Animator.runtimeAnimatorController.animationClips;
        Check(clips.All(c=>c.humanMotion&&c.isLooping),"All body animations humanoid and looping");
        Stage("physical-grips");
        foreach(var size in new[]{new Vector3(.12f,.16f,.12f),new Vector3(.5f,.45f,.4f),new Vector3(1.2f,.6f,.5f)})
        {
            sample=GameObject.CreatePrimitive(PrimitiveType.Cube);sample.name="PSX carry "+size;sample.transform.localScale=size;sample.transform.position=view.transform.TransformPoint(new Vector3(0f,-.22f,.45f));sample.AddComponent<Rigidbody>();Physics.SyncTransforms();Check(pickup.TryGrab(sample),"Grab "+size);
            foreach(float pitch in new[]{0f,40f,-40f})
            {
                SetPitch(pitch);yield return new WaitForSeconds(1.1f);Check(pickup.GetHeldObject()==sample,"Held object retained "+size+" pitch "+pitch);CheckHands("carry "+size+" pitch "+pitch);
                var target=(Transform)typeof(ProtagonistBody).GetField("rightCarryTarget",Flags).GetValue(body);Check(Vector3.Distance(body.ViewRightHand.position,target.position)<.025f,"Right grip reaches item "+size+" pitch "+pitch);
                if(pickup.IsHeldObjectTwoHanded){target=(Transform)typeof(ProtagonistBody).GetField("leftCarryTarget",Flags).GetValue(body);Check(Vector3.Distance(body.ViewLeftHand.position,target.position)<.025f,"Left grip reaches item "+size+" pitch "+pitch);}
                if(pitch==0f)Capture("carry-"+size.x);
            }
            pickup.Release();yield return new WaitForSeconds(.25f);Check(pickup.GetHeldObject()==null&&!sample.GetComponent<Rigidbody>().isKinematic,"Release restores physics "+size);Destroy(sample);sample=null;SetPitch(0f);
        }
        Stage("equipment");
        foreach(string id in new[]{"Flashlight","Crowbar","Hammer"})
        {
            if(!inventory.HasItem(id))inventory.AddItem(id);
            inventory.ActivateItem(id);yield return new WaitForSeconds(.75f);CheckHands(id);Check(inventory.HasEquippedItemAttachedToHand,"Equipment recognized "+id);Capture("equipment-"+id);
        }
        inventory.Unequip();yield return new WaitForSeconds(.4f);CheckHands("after unequip");Capture("after-unequip");
        interaction.enabled=interactionEnabled;player.mouseSensitivity=mouseSensitivity;Stage("complete");report.completedUtc=DateTime.UtcNow.ToString("O");Write();
    }
    private void CheckHands(string label)
    {
        Check(body.ViewRightHand!=null&&body.ViewLeftHand!=null,"Both view hands exist "+label);
        foreach(var hand in new[]{body.ViewRightHand,body.ViewLeftHand})
        {
            Vector3 point=view.WorldToViewportPoint(hand.position);Check(point.z>view.nearClipPlane&&point.x>0f&&point.x<1f&&point.y>0f&&point.y<1f,"Hand visible "+hand.name+" "+label+" at "+point);
            Check(float.IsFinite(hand.position.x)&&Mathf.Abs(hand.lossyScale.x/body.RightHand.lossyScale.x-1f)<.02f,"Anatomical arm scale "+hand.name+" "+label);
        }
    }
    private void Teleport(Vector3 p){var c=player.GetComponent<CharacterController>();c.enabled=false;player.transform.position=p;c.enabled=true;player.StopMotion();}
    private void SetPitch(float value)=>typeof(PlayerController).GetField("yRotation",Flags).SetValue(player,value);
    private static IEnumerator Empty(){yield break;}
    private void Check(bool ok,string label){(ok?report.passed:report.failed).Add(label);Write();}
    private void Stage(string s){report.stage=s;Write();}
    private void Write()=>File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
    private void Capture(string name)=>ProtagonistPlayProbe.Capture(view,Path.Combine(output,name+".png"));
    private void OnDisable(){if(player!=null)player.mouseSensitivity=mouseSensitivity;if(interaction!=null)interaction.enabled=interactionEnabled;if(pickup!=null)pickup.Release();if(sample!=null)Destroy(sample);Time.timeScale=1f;}
}
#endif