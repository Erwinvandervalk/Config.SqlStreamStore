using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Config.SqlStreamStore.Tests
{
    public class ConfigurationSettingsTests
    {
        public class When_modifying_settings
        {
            private readonly ConfigurationSettings _configurationSettings;
            private readonly ModifiedConfigurationSettings _modifiedConfigurationSettings;

            public When_modifying_settings()
            {
                _configurationSettings = ConfigurationSettings.Create(
                    ("1_unchanging", "initial"),
                    ("2_modified", "initial"),
                    ("3_modified_to_same_value", "initial"),
                    ("4_deleted", "initial")

                );

                _modifiedConfigurationSettings = _configurationSettings
                    .WithDeletedKeys(
                        "4_deleted")
                    .WithModifiedSettings(
                        ("2_modified", "modified"),
                        ("3_modified_to_same_value", "initial"),
                        ("5_new", "new"));
            }

            [Fact]
            public void Then_dictionary_contains_expected_values()
            {
                Assert.Equal(new Dictionary<string, string>()
                {
                    { "1_unchanging", "initial"},
                    { "2_modified", "modified"},
                    { "3_modified_to_same_value", "initial"},
                    { "5_new", "new"},
                }, _modifiedConfigurationSettings);
            }

            [Fact]
            public void Then_newvalues_contains_expected_values()
            {
                Assert.Equal(new Dictionary<string, string>()
                {
                    { "1_unchanging", "initial"},
                    { "2_modified", "modified"},
                    { "3_modified_to_same_value", "initial"},
                    { "5_new", "new"},
                }, _modifiedConfigurationSettings.NewValues);
            }
            [Fact]
            public void Then_getchanges_returns_deletions()
            {
                var deletions = _modifiedConfigurationSettings.GetChanges().DeletedSettings;
                Assert.Equal(new []{ "4_deleted" }, deletions);
            }

            [Fact]
            public void Then_getchanges_returns_modified_values()
            {
                var deletions = _modifiedConfigurationSettings.GetChanges().ModifiedSettings;

                Assert.Equal(new[] { "2_modified", "5_new" }, deletions);
            }

            [Fact]
            public void Then_not_actually_changing_values_are_not_present()
            {
                var modifications = _modifiedConfigurationSettings.GetChanges().ModifiedSettings;

                Assert.DoesNotContain("3_modified_to_same_value", modifications);
            }
        }

    }
}
