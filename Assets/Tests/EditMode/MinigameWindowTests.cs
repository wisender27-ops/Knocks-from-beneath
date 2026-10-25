using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class MinigameWindowTests
{
    private sealed class TestWindow : MinigameWindow
    {
        protected override string WindowName => "TestMinigame";
        protected override void Build(Transform root) { }
        protected override void Tick() { }
        public bool Open() => TryOpen();
        public bool Close() => CloseWindow();
        public GameObject RootObject => Root;
    }

    private readonly List<Object> _created = new List<Object>();
    private PlayerController _player;
    private PlayerInteraction _interaction;
    private bool _cameraLocked, _interactionEnabled;

    [SetUp]
    public void SetUp()
    {
        // Окно ищет игрока по сцене — работаем с тем же объектом, который оно найдёт.
        _player = Object.FindFirstObjectByType<PlayerController>();
        if (_player == null) _player = Create("Player").AddComponent<PlayerController>();
        _interaction = Object.FindFirstObjectByType<PlayerInteraction>();
        if (_interaction == null) _interaction = Create("Interaction").AddComponent<PlayerInteraction>();
        _cameraLocked = _player.isCameraLocked;
        _interactionEnabled = _interaction.enabled;
        _player.isCameraLocked = false;
        _interaction.enabled = true;
    }

    [TearDown]
    public void TearDown()
    {
        // Не оставляем статический Current открытым, если тест упал посередине.
        foreach (var obj in _created)
        {
            if (obj is GameObject go && go != null && go.TryGetComponent(out TestWindow test)) test.Close();
            if (obj is GameObject stove && stove != null && stove.TryGetComponent(out DinnerCookingWindow cooking)) cooking.ResetAttempt();
        }
        if (_player != null) _player.isCameraLocked = _cameraLocked;
        if (_interaction != null) _interaction.enabled = _interactionEnabled;
        foreach (var obj in _created)
            if (obj != null) Object.DestroyImmediate(obj);
        _created.Clear();
    }

    private GameObject Create(string name)
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go;
    }

    private TestWindow CreateWindow()
    {
        var window = Create("Window").AddComponent<TestWindow>();
        return window;
    }

    private void Track(TestWindow window)
    {
        if (window.RootObject != null) _created.Add(window.RootObject);
    }

    [Test]
    public void Open_LocksPlayerAndClose_RestoresPreviousState()
    {
        var window = CreateWindow();
        var cursorLock = Cursor.lockState;
        var cursorVisible = Cursor.visible;

        Assert.That(window.Open(), Is.True);
        Track(window);
        Assert.That(_player.isCameraLocked, Is.True);
        Assert.That(_interaction.enabled, Is.False);
        Assert.That(MinigameWindow.IsAnyOpen, Is.True);
        Assert.That(MinigameWindow.Current, Is.SameAs(window));
        Assert.That(window.RootObject.activeSelf, Is.True);

        Assert.That(window.Close(), Is.True);
        Assert.That(_player.isCameraLocked, Is.False);
        Assert.That(_interaction.enabled, Is.True);
        Assert.That(Cursor.lockState, Is.EqualTo(cursorLock));
        Assert.That(Cursor.visible, Is.EqualTo(cursorVisible));
        Assert.That(MinigameWindow.IsAnyOpen, Is.False);
        Assert.That(window.RootObject.activeSelf, Is.False);
        Assert.That(window.Close(), Is.False, "repeated close is a no-op");
    }

    [Test]
    public void Open_IsRejectedWhileAnotherMinigameIsOpen()
    {
        var first = CreateWindow();
        var second = CreateWindow();

        Assert.That(first.Open(), Is.True);
        Track(first);
        Assert.That(second.Open(), Is.False);
        Assert.That(MinigameWindow.Current, Is.SameAs(first));

        first.Close();
        Assert.That(second.Open(), Is.True);
        Track(second);
        second.Close();
    }

    [Test]
    public void Open_IsRejectedWhenCameraAlreadyLocked()
    {
        var window = CreateWindow();
        _player.isCameraLocked = true;

        Assert.That(window.Open(), Is.False);
        Assert.That(MinigameWindow.IsAnyOpen, Is.False);
        Assert.That(_interaction.enabled, Is.True, "a rejected open must not touch player state");
    }

    [Test]
    public void DinnerCooking_ResetBeforeStart_CancelsAndRestoresState()
    {
        var cooking = Create("Stove").AddComponent<DinnerCookingWindow>();
        bool completed = false, cancelled = false;

        Assert.That(cooking.Open(() => completed = true, () => cancelled = true), Is.True);
        var root = GameObject.Find("DinnerCooking");
        if (root != null) _created.Add(root);
        Assert.That(MinigameWindow.Current, Is.SameAs(cooking));
        Assert.That(_player.isCameraLocked, Is.True);

        cooking.ResetAttempt();

        Assert.That(cancelled, Is.True);
        Assert.That(completed, Is.False);
        Assert.That(cooking.IsOpen, Is.False);
        Assert.That(MinigameWindow.IsAnyOpen, Is.False);
        Assert.That(_player.isCameraLocked, Is.False);
        Assert.That(_interaction.enabled, Is.True);
        Assert.That(cooking.Open(null, null), Is.True, "reset gives the attempt back");
        cooking.ResetAttempt();
    }
}
