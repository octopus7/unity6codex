namespace CodexSix.TopdownShooter.Game
{
    public sealed class AttendanceAutoOpenCoordinator
    {
        public bool TryGetAutoOpenEventId(AttendanceUiController controller, out string eventId)
        {
            eventId = string.Empty;
            if (controller == null)
            {
                return false;
            }

            eventId = controller.GetFirstClaimableEventId();
            return !string.IsNullOrWhiteSpace(eventId);
        }
    }
}
