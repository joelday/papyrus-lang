import { ServiceCollection } from 'decoration-ioc';
import { ServiceIdentifier, IInstantiationService } from 'decoration-ioc/lib/instantiation';

export function getInstance<T>(serviceCollection: ServiceCollection, serviceIdentifier: ServiceIdentifier<T>) {
    return (serviceCollection.get(IInstantiationService) as IInstantiationService).invokeFunction((accessor) =>
        accessor.get(serviceIdentifier)
    );
}
