using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

// S-02, S-03: стука два — монстр под ногами, Хейл в трубе с подписью 2·1.
public class HaleKnockTests
{
    [TearDown]
    public void TearDown() => StoryFlags.Reset();

    [Test]
    public void PipePoint_LiesOnRouteUnderCeiling()
    {
        Vector3 point = HalePipe.NearestPoint(new Vector3(-40f, 0f, 109f));
        Assert.That(point.y, Is.EqualTo(BasementLayout.PipeY));
        Assert.That(point.x, Is.EqualTo(-40f).Within(0.001f), "Над длинным отрезком — точка прямо на нём");
        Assert.That(point.z, Is.EqualTo(111.2f).Within(0.001f));
    }

    [Test]
    public void PipePoint_DoesNotFollowPlayer()
    {
        var kitchen = new Vector3(-34f, 0f, 106f);
        Vector3 point = HalePipe.NearestPoint(kitchen);
        var flat = new Vector2(point.x - kitchen.x, point.z - kitchen.z);
        Assert.That(flat.magnitude, Is.GreaterThan(3f), "Вдали от трубы источник остаётся на трубе, а не под ногами");
    }

    [Test]
    public void PipePoint_ClampsToRouteEnds()
    {
        Vector3 point = HalePipe.NearestPoint(new Vector3(-60f, 0f, 90f));
        Assert.That(point.x, Is.EqualTo(BasementLayout.PipeRoute[0].x).Within(0.001f));
        Assert.That(point.z, Is.EqualTo(BasementLayout.PipeRoute[0].y).Within(0.001f));
    }

    [Test]
    public void HaleSignature_IsTwoThenOneWithPause()
    {
        KnockPattern signature = KnockPattern.HaleSignature;
        Assert.That(signature.TapCount, Is.EqualTo(3));
        Assert.That(signature.GapBefore(1), Is.EqualTo(KnockPattern.ShortGap));
        Assert.That(signature.GapBefore(2), Is.EqualTo(KnockPattern.LongGap));
    }

    [Test]
    public void VoicesLine_OnlyOnFirstCorrectAnswer()
    {
        Assert.That(KnockCallGame.VoicesLine(KnockCallOutcome.Correct, false), Is.Not.Null);
        Assert.That(KnockCallGame.VoicesLine(KnockCallOutcome.Correct, true), Is.Null);
        Assert.That(KnockCallGame.VoicesLine(KnockCallOutcome.Wrong, false), Is.Null);
        Assert.That(KnockCallGame.VoicesLine(KnockCallOutcome.Ignored, false), Is.Null);
    }

    [Test]
    public void KnockVoicesNoticed_ResetsWithFlags()
    {
        StoryFlags.MarkKnockVoicesNoticed();
        Assert.That(StoryFlags.KnockVoicesNoticed, Is.True);
        StoryFlags.Reset();
        Assert.That(StoryFlags.KnockVoicesNoticed, Is.False);
    }
}
