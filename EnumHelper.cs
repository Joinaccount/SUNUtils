using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace SUNUtils
{
    /// <summary>
    /// 提供枚举类型与描述特性之间的高性能解析服务
    /// <para>支持通过<see cref="DescriptionAttribute"/>实现枚举值与文本描述的双向转换</para>
    /// </summary>
    /// <remarks>
    /// <para>本类使用线程安全的缓存机制， 确保每个枚举类型仅执行一次反射操作</para>
    /// <para>所有方法均为线程安全</para>
    /// </remarks>
    public static class EnumHelper
    {
        // 线程安全的描述字典缓存
        private static readonly ConcurrentDictionary<Type, object> _descriptionCache = new();

        // 内部缓存结构
        private class EnumCache<T> where T : Enum
        {
            public Dictionary<T, string> ValueToDescription { get; } = new Dictionary<T, string>();
            public Dictionary<string, T> DescriptionToValue { get; }
                = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 通过描述获取枚举值（严格模式）
        /// </summary>
        public static T FromDescription<T>(string description) where T : Enum
        {
            var cache = GetCache<T>();
            if (cache.DescriptionToValue.TryGetValue(description, out var value))
            {
                return value;
            }
            throw new ArgumentException($"未找到描述为 '{description}' 的 {typeof(T).Name} 枚举值");
        }

        /// <summary>
        /// 安全模式获取枚举值
        /// </summary>
        public static bool TryFromDescription<T>(string description, out T value) where T : Enum
        {
            var cache = GetCache<T>();
            return cache.DescriptionToValue.TryGetValue(description, out value);
        }

        /// <summary>
        /// 获取枚举值的描述信息
        /// </summary>
        public static string GetDescription<T>(T value) where T : Enum
        {
            var cache = GetCache<T>();
            return cache.ValueToDescription.TryGetValue(value, out var description) ? description : string.Empty;
        }

        // 初始化或获取缓存
        private static EnumCache<T> GetCache<T>() where T : Enum
        {
            return (EnumCache<T>)_descriptionCache.GetOrAdd(typeof(T), type =>
            {
                var cache = new EnumCache<T>();
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!field.IsLiteral || !field.FieldType.Equals(type)) continue;

                    var attribute = field.GetCustomAttribute<DescriptionAttribute>();
                    if (attribute == null)
                    {
                        continue;
                        //throw new InvalidOperationException(
                        //    $"枚举类型 {type.Name} 的成员 {field.Name} 未配置Description特性");
                    }

                    var value = (T)field.GetValue(null);
                    if (cache.DescriptionToValue.ContainsKey(attribute.Description))
                    {
                        throw new InvalidOperationException(
                            $"枚举类型 {type.Name} 存在重复描述值: {attribute.Description}");
                    }

                    cache.DescriptionToValue[attribute.Description] = value;
                    cache.ValueToDescription[value] = attribute.Description;
                }
                return cache;
            });
        }
    }
}
