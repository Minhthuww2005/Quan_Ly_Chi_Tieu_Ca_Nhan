import WalletHeader from '../components/WalletHeader';
import BalanceCard from '../components/BalanceCard';
import QuickActions from '../components/QuickActions';
import TransactionList from '../components/TransactionList';
import BottomNavigation from '../components/BottomNavigation';

export default function Index() {
  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <WalletHeader />
      
      {/* Balance Card */}
      <BalanceCard />
      
      {/* Quick Actions */}
      <QuickActions />
      
      {/* Transaction List */}
      <TransactionList />
      
      {/* Bottom Navigation */}
      <BottomNavigation />
    </div>
  );
}