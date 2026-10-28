using NUnit.Framework;
using KnocksFromBeneath;

public class KnockHintTests
{
    [TestCase(-2, 25f, false)]
    [TestCase(2, 25f, false)]
    [TestCase(3, 25f, true)]
    [TestCase(5, 25f, true)]
    [TestCase(3, 1f, false)]
    [TestCase(3, 145f, false)]
    public void GuidanceRequiresTrustAndAudibleDistance(int trust, float squaredDistance, bool expected)
    {
        Assert.That(KnockHints.CanGuide(trust, squaredDistance), Is.EqualTo(expected));
    }
}
