import BankCard from './BankCard';

const bankCards = [
  {
    id: '1',
    cardNumber: '1111 2222 333 9012',
    holderName: 'NGUYEN VAN A',
    expiryDate: '12/28',
    balance: 15750000,
    bankName: 'MB Bank',
    cardType: 'visa' as const,
    cardLevel: 'platinum' as const
  },
  {
    id: '2',
    cardNumber: '1111 2222 333 5678',
    holderName: 'NGUYEN VAN A',
    expiryDate: '06/27',
    balance: 8500000,
    bankName: 'Techcombank',
    cardType: 'visa' as const,
    cardLevel: 'gold' as const
  },
  {
    id: '3',
    cardNumber: '1111 2222 333 3456',
    holderName: 'NGUYEN VAN A',
    expiryDate: '09/26',
    balance: 12300000,
    bankName: 'Vietcombank',
    cardType: 'mastercard' as const,
    cardLevel: 'platinum' as const
  }
];

export default function BankCardList() {
  return (
    <div className="px-4 space-y-6">
      <h2 className="text-lg font-semibold text-gray-900">Thẻ ngân hàng của bạn</h2>
      {bankCards.map((card) => (
        <BankCard
          key={card.id}
          cardNumber={card.cardNumber}
          holderName={card.holderName}
          expiryDate={card.expiryDate}
          balance={card.balance}
          bankName={card.bankName}
          cardType={card.cardType}
          cardLevel={card.cardLevel}
        />
      ))}
    </div>
  );
}