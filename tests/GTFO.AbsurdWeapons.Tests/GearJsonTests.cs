using NUnit.Framework;

namespace GTFO.AbsurdWeapons.Tests;

[TestFixture]
public class GearJsonTests
{
    // Real gear JSON from the build-34855 datablock dump: the Mastaba Fixed Blade (knife, category
    // 27) and the Omneco exp1 (HEL machine gun, category 3, fire mode 2 = auto).
    private const string Knife =
        "{\"Ver\":1,\"Name\":\"Mastaba Fixed Blade\",\"Packet\":{\"Comps\":{\"Length\":6,\"a\":{\"c\":2,\"v\":27},\"b\":{\"c\":3,\"v\":161}," +
        "\"c\":{\"c\":4,\"v\":43},\"d\":{\"c\":44,\"v\":5},\"e\":{\"c\":48,\"v\":20},\"f\":{\"c\":50,\"v\":20}}," +
        "\"MatTrans\":{\"tDecalA\":{\"scale\":0.1}},\"publicName\":{\"data\":\"Mastaba Fixed Blade\"}}}";

    private const string HelGun =
        "{\"Ver\":1,\"Name\":\"Omneco exp1\",\"Packet\":{\"Comps\":{\"Length\":5,\"a\":{\"c\":1,\"v\":2},\"b\":{\"c\":2,\"v\":3}," +
        "\"c\":{\"c\":3,\"v\":109},\"d\":{\"c\":4,\"v\":20},\"e\":{\"c\":27,\"v\":30}},\"publicName\":{\"data\":\"Omneco exp1\"}}}";

    [Test]
    public void DisplayName_MarksModdedWeapons()
    {
        Assert.That(GearJson.DisplayName("Katana"), Is.EqualTo("Katana (!)"));
    }

    [Test]
    public void CategoryId_ReadsComponentTwo()
    {
        Assert.That(GearJson.CategoryId(Knife), Is.EqualTo(27u));
        Assert.That(GearJson.CategoryId(HelGun), Is.EqualTo(3u));
    }

    [Test]
    public void CategoryId_DoesNotConfuseComponentTwentySomething()
    {
        // Component 27 (value 30) must not be read as component 2.
        const string json = "{\"Packet\":{\"Comps\":{\"a\":{\"c\":27,\"v\":30}}}}";
        Assert.That(GearJson.CategoryId(json), Is.Null);
    }

    [Test]
    public void FireModeId_DefaultsToSemiWhenAbsent()
    {
        Assert.That(GearJson.FireModeId(HelGun), Is.EqualTo(2));
        Assert.That(GearJson.FireModeId(Knife), Is.EqualTo(0));
    }

    [Test]
    public void SetComponent_ChangesOnlyThatComponent()
    {
        var held = GearJson.SetComponent(HelGun, 4, 1103);
        Assert.That(GearJson.Component(held, 4), Is.EqualTo(1103u));
        Assert.That(GearJson.Component(held, 3), Is.EqualTo(109u));
        Assert.That(GearJson.CategoryId(held), Is.EqualTo(3u));
    }

    [Test]
    public void SetComponent_LeavesJsonAloneWhenMissing()
    {
        Assert.That(GearJson.SetComponent(Knife, 1, 2), Is.EqualTo(Knife));
    }

    [Test]
    public void Rewrite_RepointsCategoryAndRenamesBothNames()
    {
        var json = GearJson.Rewrite(Knife, 27, 1009, GearJson.DisplayName("Tanto"));
        Assert.That(json, Is.Not.Null);
        Assert.That(GearJson.CategoryId(json!), Is.EqualTo(1009u));
        Assert.That(json, Does.Contain("\"Name\":\"Tanto (!)\""));
        Assert.That(json, Does.Contain("\"publicName\":{\"data\":\"Tanto (!)\"}"));
        Assert.That(json, Does.Not.Contain("Mastaba"));
        // Every other component is untouched.
        Assert.That(GearJson.Component(json!, 3), Is.EqualTo(161u));
        Assert.That(GearJson.Component(json!, 50), Is.EqualTo(20u));
    }

    [Test]
    public void Rewrite_ReturnsNullWhenTheModelIsNotInThatCategory()
    {
        Assert.That(GearJson.Rewrite(Knife, 14, 1010, "Gravity Hammer"), Is.Null);
    }

    [Test]
    public void Rewrite_DoesNotMatchALongerCategoryNumber()
    {
        // Asking for category 2 must not match the knife's 27.
        Assert.That(GearJson.Rewrite(Knife, 2, 1001, "Yari"), Is.Null);
    }

    [TestCase("Price $1")]
    [TestCase("Quote \"Q\"")]
    [TestCase("Back\\slash")]
    public void Rewrite_KeepsNamesWithSpecialCharactersIntact(string name)
    {
        var json = GearJson.Rewrite(Knife, 27, 1009, name);
        Assert.That(json, Is.Not.Null);
        Assert.That(GearJson.CategoryId(json!), Is.EqualTo(1009u));
        var escaped = name.Replace("\\", "\\\\").Replace("\"", "\\\"");
        Assert.That(json, Does.Contain($"\"Name\":\"{escaped}\""));
    }
}
