namespace VkGuide;

public struct DeletionQueue
{
    public Stack<Action> Deletors;
    public DeletionQueue()
    {
        Deletors = new ();
    }

    public void Queue(Action deletor) => Deletors.Push(deletor);
    
    
    public void Flush()
    {
        while(Deletors.Any())
        {
            var item = Deletors.Pop();
            item.Invoke();
        }
    }
}
