// 로컬 시네마틱 전용 물리 씬의 생성·고정 스텝·정리 흐름

using UnityEngine;
using UnityEngine.SceneManagement;

namespace NGPN.Gameplay
{
    public sealed class CinematicPhysicsWorld : MonoBehaviour
    {
        public static CinematicPhysicsWorld Instance { get; private set; }

        [SerializeField] private float step = 1f / 60f;

        private Scene _scene;
        private PhysicsScene _physicsScene;
        private float _accum;
        private bool _pendingCleanup;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureScene();

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureScene();
            _pendingCleanup = true;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            _pendingCleanup = true;
        }

        private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            _pendingCleanup = true;
        }

        private void EnsureScene()
        {
            if (_physicsScene.IsValid() && _scene.IsValid())
                return;

            Scene existing = SceneManager.GetSceneByName("CinematicPhysicsScene");
            if (existing.IsValid())
            {
                _scene = existing;
                _physicsScene = _scene.GetPhysicsScene();
                return;
            }

            CreateSceneParameters sp = new(LocalPhysicsMode.Physics3D);
            _scene = SceneManager.CreateScene("CinematicPhysicsScene", sp);
            _physicsScene = _scene.GetPhysicsScene();
        }

        private void Update()
        {
            EnsureScene();
            if (!_physicsScene.IsValid()) return;

            if (_pendingCleanup)
            {
                _pendingCleanup = false;
                CleanupCinematicSceneRoots();
            }

            // 게임 timeScale과 분리된 60 Hz 물리 진행
            _accum += Time.unscaledDeltaTime;
            while (_accum >= step)
            {
                if (!_physicsScene.IsValid()) break;
                _physicsScene.Simulate(step);
                _accum -= step;
            }
        }

        private void CleanupCinematicSceneRoots()
        {
            if (!_scene.IsValid())
            {
                Scene existing = SceneManager.GetSceneByName("CinematicPhysicsScene");
                if (!existing.IsValid()) return;

                _scene = existing;
                _physicsScene = _scene.GetPhysicsScene();
            }

            GameObject[] roots = _scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject go = roots[i];
                if (go == null || go == gameObject) continue;
                Destroy(go);
            }

            // 다음 씬의 첫 프레임에 이전 누적 스텝이 적용되지 않도록 초기화
            _accum = 0f;
        }

        public void MoveToCinematicScene(GameObject go)
        {
            EnsureScene();
            if (!_physicsScene.IsValid() || go == null) return;

            SceneManager.MoveGameObjectToScene(go, _scene);
        }
    }
}
