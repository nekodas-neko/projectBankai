using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Client
{
    // The first scene: loads the character and shows the home screen. Playing a mode loads its scene; the Character screen works
    // here without restarting anything, because nothing is running yet.
    public class HomeBootstrap : MonoBehaviour
    {
        public const string HomeScene = "Home";
        public const string DungeonScene = "Arena";

        MasteryProgress mastery;
        Registry<ClassDef> classes;
        Registry<AbilityDef> abilities;
        Registry<SkillTreeDef> trees;
        HomeScreen home;
        CharacterScreen characterScreen;

        public HomeScreen Home => home;

        public CharacterScreen CharacterScreen => characterScreen;

        public MasteryProgress Mastery => mastery;

        void Start()
        {
            DefinitionLoader.Load(out classes, out abilities);
            trees = SkillTreeCatalog.Load(classes, abilities);
            mastery = new MasteryProgress(ProfileStore.ForDevice(), MasteryCatalog.Load());
            mastery.UseModifiers(ModifierCatalog.Load());
            mastery.UseArmorSets(ArmorCatalog.Load());
            mastery.Equip(HudSettings.ClassName);

            var root = new GameObject("HomeCanvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var system = new GameObject("EventSystem", typeof(EventSystem));
                system.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var safeArea = Hud.NewRect("SafeArea", root.transform);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
            home = new HomeScreen(safeArea, Play, ToggleCharacter);
            characterScreen = new CharacterScreen(safeArea, Equip, TakeApart, RefreshCharacter, ToggleCharacter);
            Refresh();
        }

        void Update()
        {
            if (mastery != null)
            {
                mastery.Tick(Time.unscaledDeltaTime);
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                mastery?.Flush();
            }
        }

        void OnDestroy()
        {
            mastery?.Flush();
            BuildNumberOverlay.Extra = string.Empty;
        }

        void Refresh()
        {
            home.Show(Info());
            BuildNumberOverlay.Extra = mastery.Summary();
        }

        CharacterInfo Info()
        {
            var weaponClass = classes.TryGet(mastery.Profile.EquippedType, out var def) ? def : classes.Get(HudSettings.DefaultWeapon);
            return CharacterInfo.Build(mastery, weaponClass, abilities, LoadoutFor(weaponClass.Id));
        }

        // The skill tree choices of the weapon type, as the variants the dungeon will start with.
        int[] LoadoutFor(string weaponType)
        {
            if (!trees.TryGet(weaponType, out var tree))
            {
                return new int[Entity.AbilitySlots];
            }

            var weapon = mastery.Profile.MasteryFor(weaponType);
            SkillTreeRules.Sanitize(tree, weapon);
            return SkillTreeRules.Loadout(tree, weapon);
        }

        void Play(string mode)
        {
            if (mode != "dungeon")
            {
                return;
            }

            mastery.Flush();
            HudSettings.SessionMode = (int)SessionMode.Solo;
            SceneManager.LoadScene(DungeonScene);
        }

        void ToggleCharacter()
        {
            var open = !characterScreen.IsOpen;
            characterScreen.SetOpen(open);
            if (open)
            {
                RefreshCharacter();
            }
            else
            {
                Refresh();
            }
        }

        void RefreshCharacter()
        {
            characterScreen.Show(Info(), mastery);
        }

        // At home nothing is running, so carrying something else only changes what the next dungeon starts with.
        void Equip(CharacterScreen.Tab tab, string uid)
        {
            if (tab == CharacterScreen.Tab.Weapons)
            {
                var weapon = mastery.Profile.Weapons.Find(w => w.Uid == uid);
                if (weapon != null && mastery.EquipWeapon(uid))
                {
                    HudSettings.ClassName = weapon.Type;
                }
            }
            else
            {
                mastery.EquipArmor(uid);
            }

            RefreshCharacter();
            Refresh();
        }

        void TakeApart(CharacterScreen.Tab tab, string uid)
        {
            var done = tab == CharacterScreen.Tab.Weapons ? mastery.DismantleWeapon(uid, out _) : mastery.DismantleArmor(uid, out _);
            if (done)
            {
                RefreshCharacter();
                Refresh();
            }
        }
    }
}
