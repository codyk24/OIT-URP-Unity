using UnityEngine;

/// <summary>
/// Be aware this will not prevent a non singleton constructor
///   such as `T myT = new T();`
/// To prevent that, add `protected T () {}` to your singleton class.
/// 
/// As a note, this is made as MonoBehaviour because we need Coroutines.
/// </summary>
public abstract class BaseMonoSingleton<T> : MonoBehaviour
	where T : MonoBehaviour
{
	#region Variables

	[SerializeField]
	private bool m_dontDestroyOnLoad;

	protected bool m_destroyInAwake;
	
	private static T _instance;
	private static object _lock = new object();
	private static bool applicationIsQuitting = false;

	#endregion

	#region Properties

	public static T Instance
    {
        get
        {
			bool quitCheck = applicationIsQuitting;

#if UNITY_EDITOR
			quitCheck &= UnityEditor.EditorApplication.isPlaying;
#endif

			if (quitCheck)
            {
                Debug.LogWarning("[Singleton] Instance '" + typeof(T) +
                    "' already destroyed on application quit." +
                    " Won't create again - returning null.");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = (T)FindAnyObjectByType(typeof(T));

                    // if (FindAnyObjectByType(typeof(T), ).Length > 1)
                    // {
                    //     Debug.LogError("[Singleton] Something went really wrong " +
                    //         " - there should never be more than 1 singleton!" +
                    //         " Reopening the scene might fix it.");
                    //     return _instance;
                    // }

                    if (_instance == null)
                    {
                        GameObject singleton = new GameObject();
                        _instance = singleton.AddComponent<T>();
                        singleton.name = "(singleton) " + typeof(T).ToString();

                        DontDestroyOnLoad(singleton);

                        Debug.Log("[Singleton] An instance of " + typeof(T) +
                            " is needed in the scene, so '" + singleton +
                            "' was created with DontDestroyOnLoad.");
                    }
                    else
                    {
                        Debug.Log("[Singleton] Using instance already created: " +
                            _instance.gameObject.name);
                    }
                }

#if UNITY_EDITOR
				(_instance as BaseMonoSingleton<T>).Refresh();
#endif

				return _instance;
            }
        }
    }

	public static bool Exists => _instance != null;

	public static bool Ready
	{
		get
		{
#if UNITY_EDITOR
			return !UnityEditor.EditorApplication.isPlaying;
#else
			return Exists;
#endif
		}
	}

	#endregion

	#region Methods

	protected virtual void Awake()
	{
		applicationIsQuitting = false;

		// Instance does not exist...
		if (!applicationIsQuitting && _instance == null)
		{
			var instance = Instance;

			if (m_dontDestroyOnLoad)
			{
				DontDestroyOnLoad(gameObject);
			}
		}
		// Instance already exists...
		else if (_instance != this)
		{
			Destroy(this);
			m_destroyInAwake = true;
		}
	}

	/// <summary>
	/// When Unity quits, it destroys objects in a random order.
	/// In principle, a Singleton is only destroyed when application quits.
	/// If any script calls Instance after it have been destroyed, 
	///   it will create a buggy ghost object that will stay on the Editor scene
	///   even after stopping playing the Application. Really bad!
	/// So, this was made to be sure we're not creating that buggy ghost object.
	/// </summary>
	protected virtual void OnDestroy()
    {
		if (_instance == this)
		{
			applicationIsQuitting = true;
			_instance = null;
		}
    }

	protected virtual void Refresh() { }

	#endregion
}
