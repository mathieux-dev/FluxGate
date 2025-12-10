import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MerchantList } from './merchant-list';

// Mock the hooks
jest.mock('@/lib/hooks/use-merchants', () => ({
  useMerchants: jest.fn(() => ({
    data: {
      merchants: [
        {
          id: '1',
          name: 'Test Merchant',
          email: 'test@example.com',
          active: true,
          totalVolume: 100000,
          transactionCount: 50,
          createdAt: '2024-01-01T00:00:00Z',
        },
      ],
      total: 1,
      page: 1,
      limit: 50,
      totalPages: 1,
    },
    isLoading: false,
    error: null,
  })),
}));

jest.mock('next/navigation', () => ({
  useRouter: () => ({
    push: jest.fn(),
  }),
}));

const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

describe('MerchantList', () => {
  it('renders merchant list correctly', () => {
    const queryClient = createTestQueryClient();
    
    render(
      <QueryClientProvider client={queryClient}>
        <MerchantList filters={{}} />
      </QueryClientProvider>
    );

    expect(screen.getByText('Test Merchant')).toBeInTheDocument();
    expect(screen.getByText('test@example.com')).toBeInTheDocument();
    expect(screen.getByText('Ativo')).toBeInTheDocument();
  });
});