// <copyright file="ConfigUI.cs" company="dymanoid">
//     Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace StopsAndStations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using SkyTools.Configuration;
    using SkyTools.Localization;
    using SkyTools.UI;

    /// <summary>Manages the mod's configuration page.</summary>
    internal sealed class ConfigUI
    {
        private readonly ConfigurationProvider<ModConfiguration> configProvider;
        private readonly IEnumerable<IViewItem> viewItems;

        private ConfigUI(ConfigurationProvider<ModConfiguration> configProvider, IEnumerable<IViewItem> viewItems)
        {
            this.configProvider = configProvider;
            this.viewItems = viewItems;
            this.configProvider.Changed += ConfigProviderChanged;
        }

        /// <summary>
        /// Creates the mod's configuration page using the specified object as data source.
        /// </summary>
        /// <param name="configProvider">The mod's configuration provider.</param>
        /// <param name="itemFactory">The view item factory to use for creating the UI elements.</param>
        /// <returns>A configured instance of the <see cref="ConfigUI"/> class.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the specified <see cref="ConfigurationProvider{RealTimeConfig}"/>
        /// is not initialized yet.</exception>
        public static ConfigUI Create(ConfigurationProvider<ModConfiguration> configProvider, IViewItemFactory itemFactory)
        {
            if (configProvider == null)
            {
                throw new ArgumentNullException(nameof(configProvider));
            }

            if (itemFactory == null)
            {
                throw new ArgumentNullException(nameof(itemFactory));
            }

            if (configProvider.Configuration == null)
            {
                throw new InvalidOperationException("The configuration provider has no configuration yet");
            }

            var viewItems = new List<IViewItem>();
            CreateViewItems(configProvider, itemFactory, viewItems);

            return new ConfigUI(configProvider, viewItems);
        }

        /// <summary>Closes this instance.</summary>
        public void Close() => configProvider.Changed -= ConfigProviderChanged;

        /// <summary>Translates the UI using the specified localization provider.</summary>
        /// <param name="localizationProvider">The localization provider to use for translation.</param>
        public void Translate(ILocalizationProvider localizationProvider)
        {
            foreach (var item in viewItems)
            {
                item.Translate(localizationProvider);
            }
        }

        private static void CreateViewItems(
            ConfigurationProvider<ModConfiguration> configProvider,
            IViewItemFactory itemFactory,
            ICollection<IViewItem> viewItems)
        {
            var properties = configProvider.Configuration.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => new { Property = p, Attribute = GetCustomItemAttribute<ConfigItemAttribute>(p) })
                .Where(v => v.Attribute != null);

            foreach (var tab in properties.GroupBy(p => p.Attribute.TabId).OrderBy(p => p.Key))
            {
                var tabItem = itemFactory.CreateTabItem(tab.Key);
                viewItems.Add(tabItem);

                foreach (var group in tab.GroupBy(p => p.Attribute.GroupId).OrderBy(p => p.Key))
                {
                    IContainerViewItem containerItem;
                    if (string.IsNullOrEmpty(group.Key))
                    {
                        containerItem = tabItem;
                    }
                    else
                    {
                        containerItem = itemFactory.CreateGroup(tabItem, group.Key);
                        viewItems.Add(containerItem);
                    }

                    foreach (var item in group.OrderBy(i => i.Attribute.Order))
                    {
                        var viewItem = CreateViewItem(containerItem, item.Property, configProvider, itemFactory);
                        if (viewItem != null)
                        {
                            viewItems.Add(viewItem);
                        }
                    }
                }
            }
        }

        private static IViewItem CreateViewItem(
            IContainerViewItem container,
            PropertyInfo property,
            ConfigurationProvider<ModConfiguration> configProvider,
            IViewItemFactory itemFactory)
        {
            object Config() => configProvider.Configuration;

            if (property.PropertyType == typeof(int)
                && GetCustomItemAttribute<ConfigItemUIBaseAttribute>(property) is ConfigItemSliderAttribute slider)
            {
                return itemFactory.CreateSlider(
                    container,
                    property.Name,
                    property,
                    Config,
                    slider.Min,
                    slider.Max,
                    slider.Step,
                    slider.ValueType,
                    slider.DisplayMultiplier);
            }

            return null;
        }

        private static T GetCustomItemAttribute<T>(PropertyInfo property, bool inherit = false)
            where T : Attribute
            => (T)property.GetCustomAttributes(typeof(T), inherit).FirstOrDefault();

        private void ConfigProviderChanged(object sender, EventArgs e) => RefreshAllItems();

        private void RefreshAllItems()
        {
            foreach (var item in viewItems.OfType<IValueViewItem>())
            {
                item.Refresh();
            }
        }
    }
}
