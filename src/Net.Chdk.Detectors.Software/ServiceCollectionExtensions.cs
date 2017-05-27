﻿using Microsoft.Extensions.DependencyInjection;

namespace Net.Chdk.Detectors.Software
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<ISoftwareDetector, SoftwareDetector>();
        }

        public static IServiceCollection AddMetadataSoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<IInnerSoftwareDetector, MetadataSoftwareDetector>();
        }

        public static IServiceCollection AddBinarySoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<IInnerSoftwareDetector, BinarySoftwareDetector>()
                .AddSingleton<IBinarySoftwareDetector, BinarySoftwareDetector>();
        }

        public static IServiceCollection AddEosBinarySoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<IInnerBinarySoftwareDetector, EosBinarySoftwareDetector>();
        }

        public static IServiceCollection AddKnownPsBinarySoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<IInnerBinarySoftwareDetector, KnownPsBinarySoftwareDetector>();
        }

        public static IServiceCollection AddUnkownPsBinarySoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<IInnerBinarySoftwareDetector, UnknownPsBinarySoftwareDetector>();
        }

        public static IServiceCollection AddFileSystemSoftwareDetector(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<IInnerSoftwareDetector, FileSystemSoftwareDetector>();
        }
    }
}
