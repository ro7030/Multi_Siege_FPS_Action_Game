namespace ProjectM.UI
{
    /// <summary>
    /// UI(상점 등)가 gameplay 입력을 점유할 때 PlayerController 등이 참조한다.
    /// </summary>
    public static class UIInputModal
    {
        public static bool IsBlockingGameplayInput { get; private set; }

        public static void Push() => IsBlockingGameplayInput = true;

        public static void Pop() => IsBlockingGameplayInput = false;
    }
}
