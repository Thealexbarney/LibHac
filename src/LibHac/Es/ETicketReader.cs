using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Spl;

namespace LibHac.Es
{
    public class ETicketReader
    {
        public ETicket Ticket;

        public ETicketReader() { }

        public ETicketReader(ETicket ticket)
        {
            Ticket = ticket;
        }

        public ETicketReader(ReadOnlySpan<byte> ticketData)
        {
            if (ticketData.Length < Unsafe.SizeOf<ETicket>())
            {
                throw new ArgumentException($"{nameof(ticketData)} must be at least {Unsafe.SizeOf<ETicket>()} bytes long.");
            }

            Ticket = Unsafe.As<byte, ETicket>(ref MemoryMarshal.GetReference(ticketData));
        }

        public AccessKey GetTitleKey(Keyset keyset)
        {
            if (Ticket.TitleKeyType == TitleKeyType.Common)
            {
                return Unsafe.As<byte, AccessKey>(ref MemoryMarshal.GetReference(Ticket.TitleKey));
            }

            return new AccessKey(CryptoOld.DecryptTitleKey(Ticket.TitleKey.ToArray(), keyset.EticketExtKeyRsa));
        }
    }
}
