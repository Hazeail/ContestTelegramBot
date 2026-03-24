using System;

namespace ContestBot
{
    internal class Contest
    {
        public int Id { get; set; }                   // идентификатор конкурса
        public string Code { get; set; }              // короткий код конкурса: c1, c2, ...

        public string Name { get; set; }              // название конкурса
        public string Type { get; set; }              // "normal" или "referral"
        public string Description { get; set; }       // описание (caption)

        public DateTime StartAt { get; set; }         // когда начинается
        public DateTime EndAt { get; set; }           // когда заканчивается (дата розыгрыша)

        public string Status { get; set; }            // "Draft", "Running", "Finished"

        public double BaseWeight { get; set; }        // базовый вес за участие
        public double PerReferralWeight { get; set; } // вес за каждого реферала
        public double MaxWeight { get; set; }         // максимум

        public int WinnersCount { get; set; }         // сколько победителей

        // ---- Media for channel post (one message: media + caption) ----
        // photo / animation / video / none
        public string MediaType { get; set; }         // тип медиа
        public string MediaFileId { get; set; }       // Telegram file_id

        // Backward compatibility (старое поле, можно постепенно убрать)
        public string ImageFileId { get; set; }       // legacy: file_id (photo)

        // ---- Target channel for this contest ----
        public long? ChannelId { get; set; }         // куда публиковать конкурс
        public string ChannelUsername { get; set; }  // @username без "@", опционально

        // ---- Published channel post ----
        public int? ChannelPostMessageId { get; set; } // MessageId поста в канале (для обновлений)

        // ---- Owner admin (who created the contest) ----
        public long? CreatedByAdminUserId { get; set; } // только создатель; null у старых конкурсов
    }
}
