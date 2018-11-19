using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LibHac.Npdm
{
    public class KernelAccessControl
    {
        public List<KernelAccessControlItem> Items { get; }

        public KernelAccessControl(Stream stream, int offset, int size)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            var reader = new BinaryReader(stream);

            var items = new KernelAccessControlItem[size / 4];

            for (int index = 0; index < size / 4; index++)
            {
                uint descriptor = reader.ReadUInt32();

                //Ignore the descriptor.
                if (descriptor == 0xffffffff)
                {
                    continue;
                }

                items[index] = new KernelAccessControlItem();

                int lowBits = 0;

                while ((descriptor & 1) != 0)
                {
                    descriptor >>= 1;

                    lowBits++;
                }

                descriptor >>= 1;

                switch (lowBits)
                {
                    //Kernel flags.
                    case 3:
                        {
                            items[index].HasKernelFlags = true;

                            items[index].HighestThreadPriority = (descriptor >> 0) & 0x3f;
                            items[index].LowestThreadPriority = (descriptor >> 6) & 0x3f;
                            items[index].LowestCpuId = (descriptor >> 12) & 0xff;
                            items[index].HighestCpuId = (descriptor >> 20) & 0xff;

                            break;
                        }

                    //Syscall mask.
                    case 4:
                        {
                            items[index].HasSvcFlags = true;

                            items[index].AllowedSvcs = new bool[0x80];

                            int sysCallBase = (int)(descriptor >> 24) * 0x18;

                            for (int sysCall = 0; sysCall < 0x18 && sysCallBase + sysCall < 0x80; sysCall++)
                            {
                                items[index].AllowedSvcs[sysCallBase + sysCall] = (descriptor & 1) != 0;

                                descriptor >>= 1;
                            }

                            break;
                        }

                    //Map IO/Normal.
                    case 6:
                        {
                            ulong address = (descriptor & 0xffffff) << 12;
                            bool isRo = (descriptor >> 24) != 0;

                            if (index == size / 4 - 1)
                            {
                                throw new Exception("Invalid Kernel Access Control Descriptors!");
                            }

                            descriptor = reader.ReadUInt32();

                            if ((descriptor & 0x7f) != 0x3f)
                            {
                                throw new Exception("Invalid Kernel Access Control Descriptors!");
                            }

                            descriptor >>= 7;

                            ulong mmioSize = (descriptor & 0xffffff) << 12;
                            bool isNormal = (descriptor >> 24) != 0;

                            items[index].NormalMmio.Add(new KernelAccessControlMmio(address, mmioSize, isRo, isNormal));

                            index++;

                            break;
                        }

                    //Map Normal Page.
                    case 7:
                        {
                            ulong address = descriptor << 12;

                            items[index].PageMmio.Add(new KernelAccessControlMmio(address, 0x1000, false, false));

                            break;
                        }

                    //IRQ Pair.
                    case 11:
                        {
                            items[index].Irq.Add(new KernelAccessControlIrq(
                                (descriptor >> 0) & 0x3ff,
                                (descriptor >> 10) & 0x3ff));

                            break;
                        }

                    //Application Type.
                    case 13:
                        {
                            items[index].HasApplicationType = true;

                            items[index].ApplicationType = (int)descriptor & 7;

                            break;
                        }

                    //Kernel Release Version.
                    case 14:
                        {
                            items[index].HasKernelVersion = true;

                            items[index].KernelVersionRelease = (int)descriptor;

                            break;
                        }

                    //Handle Table Size.
                    case 15:
                        {
                            items[index].HasHandleTableSize = true;

                            items[index].HandleTableSize = (int)descriptor;

                            break;
                        }

                    //Debug Flags.
                    case 16:
                        {
                            items[index].HasDebugFlags = true;

                            items[index].AllowDebug = ((descriptor >> 0) & 1) != 0;
                            items[index].ForceDebug = ((descriptor >> 1) & 1) != 0;

                            break;
                        }
                }
            }

            Items = items.ToList();
        }
    }
}
