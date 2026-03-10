namespace OperationalPlanMS.Models
{
    /// <summary>
    /// حالة تأكيد الخطوة
    /// </summary>
    public enum ApprovalStatus
    {
        /// <summary>
        /// لم تُرسل للتأكيد
        /// </summary>
        None = 0,

        /// <summary>
        /// بانتظار التأكيد
        /// </summary>
        Pending = 1,

        /// <summary>
        /// مؤكدة
        /// </summary>
        Approved = 2,

        /// <summary>
        /// مرفوضة
        /// </summary>
        Rejected = 3
    }
}
