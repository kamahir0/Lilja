using UnityEditor.IMGUI.Controls;

namespace Lilja.Repository.Editor
{
    public class RepositoryTrackerViewItem : TreeViewItem<int>
    {
        public string RepositoryName { get; set; }
        public string Key { get; set; }
        public string Type { get; set; }
        public string ValuePreview { get; set; }
        public object FullValue { get; set; }
        public bool IsRepository { get; set; }
        public int ItemCount { get; set; }

        public RepositoryTrackerViewItem(int id) : base(id)
        {
        }
    }
}
