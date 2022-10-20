using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.Npdm;

namespace hactoolnet;

internal static class ProcessNpdm
{
    public static void Process(Context ctx)
    {
        using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
        {
            var npdm = new NpdmBinary(file.AsStream(), ctx.KeySet);

            if(ctx.Options.JsonFile != null)
            {
                string json = JsonSerializer.Serialize(new
                {
                    name = npdm.TitleName,
                    title_id = hex_string(npdm.Aci0.TitleId, 16),
                    title_id_range_min = hex_string(npdm.AciD.TitleIdRangeMin, 16),
                    title_id_range_max = hex_string(npdm.AciD.TitleIdRangeMax, 16),
                    main_thread_stack_size = hex_string(npdm.MainEntrypointStackSize, 8),
                    main_thread_priority = npdm.MainThreadPriority,
                    default_cpu_id = npdm.DefaultCpuId,
                    version = npdm.ProcessCategory,
                    is_retail = (npdm.AciD.Flags & 1) == 1,
                    pool_partition = (npdm.AciD.Flags >> 2) & 3,
                    is_64_bit = npdm.Is64Bits,
                    address_space_type = npdm.AddressSpaceWidth,

                    filesystem_access = new { permissions = hex_string(npdm.Aci0.FsPermissionsBitmask, 16) },

                    service_access = npdm.Aci0.ServiceAccess.Services.FindAll(delegate(Tuple<string, bool> s) { return !s.Item2; }).ConvertAll(delegate(Tuple<string, bool> s) { return s.Item1; }),
                    service_host = npdm.Aci0.ServiceAccess.Services.FindAll(delegate (Tuple<string, bool> s) { return s.Item2; }).ConvertAll(delegate (Tuple<string, bool> s) { return s.Item1; }),

                    kernel_capabilities = kac_get_json(npdm.Aci0.KernelAccess)

                }, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ctx.Options.JsonFile, json);
            }
        }
    }

    private static List<object> kac_get_json(KernelAccessControl kac)
    {
        var list = new List<object>();
        Dictionary<string, string> syscall_memory = null;

        kac.Items.ForEach(delegate (KernelAccessControlItem item)
        {
            switch(item.LowBits)
            {
                case 3:
                {
                    list.Add(kac_create_obj("kernel_flags", new
                    {
                        highest_thread_priority = item.HighestThreadPriority,
                        lowest_thread_priority = item.LowestThreadPriority,
                        lowest_cpu_id = item.LowestCpuId,
                        highest_cpu_id = item.HighestCpuId
                    }));
                    break;
                }
                case 4:
                {
                    if(syscall_memory == null)
                    {
                        syscall_memory = new Dictionary<string, string>();
                        list.Add(kac_create_obj("syscalls", syscall_memory));
                    }
                    for (int i = 0; i < item.AllowedSvcs.Length; i++)
                    {
                        if (item.AllowedSvcs[i])
                            syscall_memory.Add("svc" + ((SvcName)i), hex_string(i, 2));
                    }
                    break;
                }
                case 6:
                {
                    var mmio = item.NormalMmio[0]; // TODO what happens with multiple Mmios?
                    list.Add(kac_create_obj("map", new
                    {
                        address = mmio.Address,
                        is_ro = mmio.IsRo,
                        size = mmio.Size,
                        is_io = mmio.IsNormal
                    }));
                    break;
                }
                case 7:
                {
                    var mmio = item.PageMmio[0]; // TODO what happens with multiple Mmios?
                    list.Add(kac_create_obj("map_page", mmio.Address));
                    break;
                }
                case 11:
                {
                    var irq = item.Irq[0]; // TODO what happens with multiple IRQ?
                    list.Add(kac_create_obj("irq_pair", new uint[] { irq.Irq0, irq.Irq1 })); // ignoring null checks/values
                    break;
                }
                case 13:
                {
                    list.Add(kac_create_obj("application_type", item.ApplicationType));
                    break;
                }
                case 14:
                {
                    list.Add(kac_create_obj("min_kernel_version", hex_string(item.KernelVersionRelease, 4)));
                    break;
                }
                case 15:
                {
                    list.Add(kac_create_obj("handle_table_size", item.HandleTableSize));
                    break;
                }
                case 16:
                {
                    list.Add(kac_create_obj("debug_flags", new
                    {
                        allow_debug = item.AllowDebug,
                        force_debug = item.ForceDebug
                    }));
                    break;
                }
            }
        });

        return list;
    }

    private static object kac_create_obj(string type, object value)
    {
        return new { type = type, value = value };
    }

    private static string hex_string(long i, int length)
    {
        return "0x" + i.ToString("x" + length);
    }
    private static string hex_string(ulong i, int length)
    {
        return "0x" + i.ToString("x" + length);
    }
}