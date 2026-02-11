using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Lilja.Repository;
using Lilja.Repository.Test.Repositories;

namespace Lilja.Repository.Test
{
    public class RepositoryTestController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private InputField _idInput;
        [SerializeField] private InputField _valueXInput;
        [SerializeField] private InputField _valueYInput;
        [SerializeField] private InputField _descInput;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private Text _logText;

        private JsonTestEntityRepository _repository;
        private TxManager _txManager;

        private void Start()
        {
            _txManager = new TxManager();
            _repository = new JsonTestEntityRepository();

            _saveButton.onClick.AddListener(() => SaveAsync().Forget());
            _loadButton.onClick.AddListener(() => LoadAsync().Forget());
            _deleteButton.onClick.AddListener(() => DeleteAsync().Forget());

            InitializeAsync().Forget();
        }

        private async UniTaskVoid InitializeAsync()
        {
            Log("Initializing Repository...");
            await _repository.InitializeAsync();
            Log("Repository Initialized.");
        }

        private async UniTaskVoid SaveAsync()
        {
            var id = _idInput.text;
            if (string.IsNullOrEmpty(id))
            {
                Log("Error: ID is empty.");
                return;
            }

            int.TryParse(_valueXInput.text, out var x);
            int.TryParse(_valueYInput.text, out var y);
            var desc = _descInput.text;

            var entity = new TestEntity(id, new TestValueObject(x, y), desc);

            Log($"Saving Entity: {id}, ({x}, {y}), {desc}");

            try
            {
                await _txManager.BeginRWTransactionAsync(tx =>
                {
                    var existing = _repository.Read(tx, id);
                    if (existing != null)
                    {
                        _repository.Update(tx, entity);
                        Log("Entity Updated.");
                    }
                    else
                    {
                        _repository.Create(tx, entity);
                        Log("Entity Created.");
                    }
                });
                Log("Save Complete.");
            }
            catch (System.Exception ex)
            {
                Log($"Save Failed: {ex.Message}");
            }
        }

        private async UniTaskVoid LoadAsync()
        {
            var id = _idInput.text;
            if (string.IsNullOrEmpty(id))
            {
                Log("Error: ID is empty.");
                return;
            }

            Log($"Loading Entity: {id}");

            try
            {
                TestEntity entity = null;
                _txManager.BeginROTransaction(tx =>
                {
                    entity = _repository.Read(tx, id);
                });

                if (entity != null)
                {
                    _valueXInput.text = entity.Value.X.ToString();
                    _valueYInput.text = entity.Value.Y.ToString();
                    _descInput.text = entity.Description;
                    Log($"Loaded: {entity.Id}, ({entity.Value.X}, {entity.Value.Y}), {entity.Description}");
                }
                else
                {
                    Log("Entity Not Found.");
                }
            }
            catch (System.Exception ex)
            {
                Log($"Load Failed: {ex.Message}");
            }
        }

        private async UniTaskVoid DeleteAsync()
        {
            var id = _idInput.text;
            if (string.IsNullOrEmpty(id))
            {
                Log("Error: ID is empty.");
                return;
            }

            Log($"Deleting Entity: {id}");

            try
            {
                await _txManager.BeginRWTransactionAsync(tx =>
                {
                    _repository.Delete(tx, id);
                });
                Log("Delete Complete.");
            }
            catch (System.Exception ex)
            {
                Log($"Delete Failed: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            Debug.Log(message);
            if (_logText != null)
            {
                _logText.text = message + "\n" + _logText.text;
                // Keep only last 10 lines
                var lines = _logText.text.Split('\n');
                if (lines.Length > 10)
                {
                    _logText.text = string.Join("\n", lines, 0, 10);
                }
            }
        }
    }
}
