﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kentico.Kontent.Delivery.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Kentico.Kontent.Delivery.Caching
{
    /// <summary>
    /// Cache responses against the Kentico Kontent Delivery API.
    /// </summary>
    public sealed class MemoryCacheManager : IDeliveryCacheManager
    {
        private readonly IMemoryCache _memoryCache;
        private readonly DeliveryCacheOptions _cacheOptions;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _createLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly ConcurrentDictionary<string, object> _dependencyLocks = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of <see cref="MemoryCacheManager"/>
        /// </summary>
        /// <param name="memoryCache">An instance of an object that represent memory cache</param>
        /// <param name="cacheOptions">The settings of the cache</param>
        public MemoryCacheManager(IMemoryCache memoryCache, IOptions<DeliveryCacheOptions> cacheOptions)
        {
            _memoryCache = memoryCache;
            _cacheOptions = cacheOptions.Value ?? new DeliveryCacheOptions();
        }

        /// <summary>
        /// Returns or Adds data to the cache
        /// </summary>
        /// <typeparam name="T">A generic type</typeparam>
        /// <param name="key">A cache key</param>
        /// <param name="valueFactory">A factory which returns a data</param>
        /// <param name="shouldCache"></param>
        /// <param name="dependenciesFactory"></param>
        /// <returns>The data of generic type</returns>
        public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> valueFactory, Func<T, bool> shouldCache = null, Func<T, IEnumerable<string>> dependenciesFactory = null) where T : class
        {
            var attempt = await TryGetAsync<T>(key);
            if (attempt.Success)
            {
                return attempt.Value;
            }

            var entryLock = _createLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            try
            {
                await entryLock.WaitAsync();
                attempt = await TryGetAsync<T>(key);
                if (attempt.Success)
                {
                    return attempt.Value;
                }

                var value = await valueFactory();

                // Decide if the value should be cached based on the response
                if (shouldCache != null && !shouldCache(value))
                {
                    return value;
                }

                // Set different timeout for stale content
                var valueCacheOptions = new MemoryCacheEntryOptions();
                if (value is IResponse ar && ar.ApiResponse.HasStaleContent)
                {
                    valueCacheOptions.SetAbsoluteExpiration(_cacheOptions.StaleContentExpiration);
                }
                else
                {
                    valueCacheOptions.SetSlidingExpiration(_cacheOptions.DefaultExpiration);
                }

                var dependencies = dependenciesFactory?.Invoke(value) ?? new List<string>();
                var dependencyCacheOptions = new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove };
                foreach (var dependency in dependencies)
                {
                    var dependencyKey = dependency;
                    var dependencyLock = _dependencyLocks.GetOrAdd(dependencyKey, _ => new object());

                    if (!_memoryCache.TryGetValue(dependencyKey, out CancellationTokenSource tokenSource) || tokenSource.IsCancellationRequested)
                    {
                        lock (dependencyLock)
                        {
                            if (!_memoryCache.TryGetValue(dependencyKey, out tokenSource) || tokenSource.IsCancellationRequested)
                            {
                                tokenSource = _memoryCache.Set(dependencyKey, new CancellationTokenSource(), dependencyCacheOptions);
                            }
                        }
                    }

                    if (tokenSource != null)
                    {
                        valueCacheOptions.AddExpirationToken(new CancellationChangeToken(tokenSource.Token));
                    }
                }

                return _memoryCache.Set(key, value, valueCacheOptions);
            }
            finally
            {
                entryLock.Release();
            }
        }
        /// <summary>
        /// Attemptes to retrieve data from cache.
        /// </summary>
        /// <typeparam name="T">Type of the response used for deserialization</typeparam>
        /// <param name="key">A cache key</param>
        /// <returns>Returns true along with the deserialized value if the retrieval attempt was successful. Otherwise, returns false and null for the value.</returns>
        public Task<(bool Success, T Value)> TryGetAsync<T>(string key) where T : class
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var result = _memoryCache.TryGetValue(key, out object value);
            return Task.FromResult((Success: result, Value: value as T));
        }

        /// <summary>
        /// Invalidates data by the key
        /// </summary>
        /// <param name="key">A cache key</param>
        /// <returns></returns>
        public async Task InvalidateDependencyAsync(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var (Success, Value) = await TryGetAsync<CancellationTokenSource>(key);
            if (Success)
            {
                if (Value is CancellationTokenSource)
                {
                    // Invalidate by item dependency
                    Value.Cancel();
                }
                else
                {
                    // Invalidate the item itself
                    _memoryCache.Remove(key);
                }
            }
        }

        /// <summary>
        /// Clears cache
        /// </summary>
        /// <returns></returns>
        public Task ClearAsync()
        {
            foreach (var key in _createLocks.Keys)
            {
                _memoryCache.Remove(key);
            }

            return Task.CompletedTask;
        }
    }
}
